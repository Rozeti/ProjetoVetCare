using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class VacinaRepository : IVacinaRepository
    {
        /// <summary>Janela em que uma dose passa a contar como "a vencer".</summary>
        public const int DiasDeAntecedenciaDoAviso = 30;

        private readonly AppDbContext _context;

        public VacinaRepository(AppDbContext context) => _context = context;

        /// <summary>
        /// As datas da carteira são gravadas como meia-noite UTC do dia de calendário; o
        /// "hoje" da clínica precisa ir para o banco com o mesmo Kind para a comparação valer.
        /// </summary>
        private static DateTime HojeUtc => DateTime.SpecifyKind(RelogioDaClinica.Padrao.Hoje, DateTimeKind.Utc);

        public async Task Adicionar(Vacina vacina) => await _context.Vacinas.AddAsync(vacina);

        public async Task<Vacina?> ObterPorId(Guid id)
        {
            return await _context.Vacinas
                .Include(v => v.Paciente).ThenInclude(p => p!.Tutor).ThenInclude(t => t!.Usuario)
                .Include(v => v.Veterinario).ThenInclude(vet => vet!.Usuario)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<List<VacinaComSituacao>> ObterPorPaciente(Guid pacienteId)
        {
            var itens = await ComSituacao(DaCarteira(pacienteId))
                .OrderByDescending(x => x.Vacina.DataAplicacao)
                .ThenByDescending(x => x.Vacina.DataRegistro)
                .ToListAsync();

            return itens.Select(x => new VacinaComSituacao(x.Vacina, x.DoseSeguinteAplicada)).ToList();
        }

        public async Task<PaginaDe<VacinaComSituacao>> ListarPorPaciente(
            Guid pacienteId, FiltroDeVacinas filtro, ParametrosPagina parametros)
        {
            var consulta = DaCarteira(pacienteId);

            if (!string.IsNullOrWhiteSpace(filtro.Tipo))
            {
                consulta = consulta.Where(v => v.Tipo == filtro.Tipo);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Busca))
            {
                var termo = filtro.Busca.Trim().ToLowerInvariant();

                consulta = consulta.Where(v =>
                    v.Nome.ToLower().Contains(termo) ||
                    v.Fabricante.ToLower().Contains(termo) ||
                    v.Lote.ToLower().Contains(termo));
            }

            var projetada = ComSituacao(consulta);
            var hoje = HojeUtc;
            var limiteAviso = hoje.AddDays(DiasDeAntecedenciaDoAviso);

            // A situação é calculada a partir da data, então o filtro vira uma faixa de datas.
            projetada = filtro.Situacao switch
            {
                "Dose única" => projetada.Where(x => x.Vacina.ProximaDose == null),
                "Concluída" => projetada.Where(x => x.DoseSeguinteAplicada),
                "Vencida" => projetada.Where(x =>
                    !x.DoseSeguinteAplicada && x.Vacina.ProximaDose != null && x.Vacina.ProximaDose < hoje),
                "A vencer" => projetada.Where(x =>
                    !x.DoseSeguinteAplicada && x.Vacina.ProximaDose != null &&
                    x.Vacina.ProximaDose >= hoje && x.Vacina.ProximaDose <= limiteAviso),
                "Em dia" => projetada.Where(x =>
                    !x.DoseSeguinteAplicada && x.Vacina.ProximaDose != null && x.Vacina.ProximaDose > limiteAviso),
                _ => projetada
            };

            var pagina = await projetada
                .OrderByDescending(x => x.Vacina.DataAplicacao)
                .ThenByDescending(x => x.Vacina.DataRegistro)
                .Paginar(parametros);

            return pagina.Converter(x => new VacinaComSituacao(x.Vacina, x.DoseSeguinteAplicada));
        }

        public async Task<Vacina?> ObterUltimaAplicacaoDoProduto(Guid pacienteId, string nome, DateTime antesDe, Guid? ignorarId = null)
        {
            var produto = nome.Trim().ToLowerInvariant();

            return await _context.Vacinas
                .AsNoTracking()
                .Where(v => v.PacienteId == pacienteId &&
                            v.Nome.ToLower() == produto &&
                            v.DataAplicacao < antesDe &&
                            (ignorarId == null || v.Id != ignorarId))
                .OrderByDescending(v => v.DataAplicacao)
                .ThenByDescending(v => v.DataRegistro)
                .FirstOrDefaultAsync();
        }

        public async Task<List<VacinaComSituacao>> ObterVencendo(Guid clinicaId, int diasDeAntecedencia)
        {
            var limite = HojeUtc.AddDays(diasDeAntecedencia);

            var consulta = SemDoseSeguinte(_context.Vacinas
                .AsNoTracking()
                .Include(v => v.Paciente).ThenInclude(p => p!.Tutor).ThenInclude(t => t!.Usuario)
                .Include(v => v.Veterinario).ThenInclude(vet => vet!.Usuario)
                .Where(v => v.ProximaDose != null &&
                            v.ProximaDose <= limite &&
                            v.Paciente!.ClinicaId == clinicaId &&
                            v.Paciente.Ativo));

            var itens = await consulta.OrderBy(v => v.ProximaDose).ToListAsync();

            return itens.Select(v => new VacinaComSituacao(v, false)).ToList();
        }

        public async Task<List<Vacina>> ObterPendentesDeLembrete(DateTime limite)
        {
            // Uma dose cujo reforço já foi aplicado não gera aviso: o tutor já cuidou dela.
            return await SemDoseSeguinte(_context.Vacinas
                    .Include(v => v.Paciente).ThenInclude(p => p!.Tutor)
                    .Where(v => !v.LembreteEnviado &&
                                v.ProximaDose != null &&
                                v.ProximaDose <= limite &&
                                v.Paciente!.Ativo))
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, int>> ContarVencidasPorPaciente(IEnumerable<Guid> pacienteIds)
        {
            var ids = pacienteIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var hoje = HojeUtc;

            var contagens = await SemDoseSeguinte(_context.Vacinas
                    .AsNoTracking()
                    .Where(v => ids.Contains(v.PacienteId) && v.ProximaDose != null && v.ProximaDose < hoje))
                .GroupBy(v => v.PacienteId)
                .Select(g => new { PacienteId = g.Key, Total = g.Count() })
                .ToListAsync();

            return contagens.ToDictionary(c => c.PacienteId, c => c.Total);
        }

        public void Atualizar(Vacina vacina) => _context.Vacinas.Update(vacina);

        public void Remover(Vacina vacina) => _context.Vacinas.Remove(vacina);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();

        private IQueryable<Vacina> DaCarteira(Guid pacienteId) =>
            _context.Vacinas
                .AsNoTracking()
                .Include(v => v.Veterinario).ThenInclude(vet => vet!.Usuario)
                .Where(v => v.PacienteId == pacienteId);

        /// <summary>
        /// Marca cada aplicação com "a dose seguinte deste produto já foi registrada", o
        /// que encerra a pendência da dose anterior. Produto é o nome, sem diferenciar
        /// maiúsculas; duas aplicações no mesmo dia se ordenam pela hora do registro.
        /// </summary>
        private IQueryable<ItemProjetado> ComSituacao(IQueryable<Vacina> consulta) =>
            consulta.Select(v => new ItemProjetado
            {
                Vacina = v,
                DoseSeguinteAplicada = v.ProximaDose != null && _context.Vacinas.Any(o =>
                    o.PacienteId == v.PacienteId &&
                    o.Id != v.Id &&
                    o.Nome.ToLower() == v.Nome.ToLower() &&
                    (o.DataAplicacao > v.DataAplicacao ||
                     (o.DataAplicacao == v.DataAplicacao && o.DataRegistro > v.DataRegistro)))
            });

        /// <summary>Só as doses cuja pendência continua em aberto.</summary>
        private IQueryable<Vacina> SemDoseSeguinte(IQueryable<Vacina> consulta) =>
            consulta.Where(v => !_context.Vacinas.Any(o =>
                o.PacienteId == v.PacienteId &&
                o.Id != v.Id &&
                o.Nome.ToLower() == v.Nome.ToLower() &&
                (o.DataAplicacao > v.DataAplicacao ||
                 (o.DataAplicacao == v.DataAplicacao && o.DataRegistro > v.DataRegistro))));

        /// <summary>Forma intermediária da consulta: o EF filtra e pagina por estes membros.</summary>
        private sealed class ItemProjetado
        {
            public Vacina Vacina { get; init; } = null!;
            public bool DoseSeguinteAplicada { get; init; }
        }
    }
}
