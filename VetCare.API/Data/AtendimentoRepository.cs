using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class AtendimentoRepository : IAtendimentoRepository
    {
        private readonly AppDbContext _context;

        public AtendimentoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(AtendimentoFisioterapeutico atendimento)
        {
            await _context.Atendimentos.AddAsync(atendimento);
        }

        public async Task<AtendimentoFisioterapeutico?> ObterPorId(Guid id)
        {
            return await _context.Atendimentos
                .Include(a => a.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Include(a => a.Sessao)
                    .ThenInclude(s => s!.Tratamento)
                        .ThenInclude(t => t!.Paciente)
                            .ThenInclude(p => p!.Tutor)
                .Include(a => a.Tratamento)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<int> ContarPorTratamento(Guid tratamentoId) =>
            await _context.Atendimentos.CountAsync(a => a.TratamentoId == tratamentoId);

        public async Task<Dictionary<Guid, int>> ContarPorTratamentos(IEnumerable<Guid> tratamentosIds)
        {
            var ids = tratamentosIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            return await _context.Atendimentos
                .AsNoTracking()
                .Where(a => ids.Contains(a.TratamentoId))
                .GroupBy(a => a.TratamentoId)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Total);
        }

        public async Task<PaginaDe<AtendimentoFisioterapeutico>> Listar(
            Guid clinicaId,
            FiltroDeEvolucoes filtro,
            ParametrosPagina parametros)
        {
            var consulta = _context.Atendimentos
                .AsNoTracking()
                .Include(a => a.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                .Include(a => a.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(a => a.Tratamento!.Paciente!.ClinicaId == clinicaId);

            if (filtro.PacienteId.HasValue)
            {
                consulta = consulta.Where(a => a.Tratamento!.PacienteId == filtro.PacienteId.Value);
            }

            if (filtro.TratamentoId.HasValue)
            {
                consulta = consulta.Where(a => a.TratamentoId == filtro.TratamentoId.Value);
            }

            if (filtro.VeterinarioId.HasValue)
            {
                consulta = consulta.Where(a => a.VeterinarioId == filtro.VeterinarioId.Value);
            }

            if (filtro.NumeroSessao.HasValue)
            {
                consulta = consulta.Where(a => a.NumeroSessao == filtro.NumeroSessao.Value);
            }

            if (filtro.InicioUtc.HasValue)
            {
                consulta = consulta.Where(a => a.DataRegistro >= filtro.InicioUtc.Value);
            }

            if (filtro.FimUtc.HasValue)
            {
                consulta = consulta.Where(a => a.DataRegistro < filtro.FimUtc.Value);
            }

            // Dentro de um tratamento as sessões seguem a ordem em que aconteceram; na visão
            // geral, as mais recentes vêm primeiro.
            var ordenada = filtro.OrdemCronologica
                ? consulta.OrderBy(a => a.DataRegistro).ThenBy(a => a.NumeroSessao)
                : consulta.OrderByDescending(a => a.DataRegistro).ThenByDescending(a => a.NumeroSessao);

            return await ordenada.Paginar(parametros);
        }

        public async Task<AtendimentoFisioterapeutico?> ObterPorSessao(Guid sessaoId)
        {
            return await _context.Atendimentos.FirstOrDefaultAsync(a => a.SessaoId == sessaoId);
        }

        public async Task<HashSet<Guid>> ObterSessoesComAtendimento(IEnumerable<Guid> sessoesIds)
        {
            var ids = sessoesIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var encontradas = await _context.Atendimentos
                .AsNoTracking()
                .Where(a => ids.Contains(a.SessaoId))
                .Select(a => a.SessaoId)
                .ToListAsync();

            return encontradas.ToHashSet();
        }

        public void Atualizar(AtendimentoFisioterapeutico atendimento)
        {
            _context.Atendimentos.Update(atendimento);
        }

        public async Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim)
        {
            return await MontarConsultaPorPeriodo(clinicaId, veterinarioId, inicio, fim).CountAsync();
        }

        private IQueryable<AtendimentoFisioterapeutico> MontarConsultaPorPeriodo(
            Guid clinicaId,
            Guid? veterinarioId,
            DateTime inicio,
            DateTime fim)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            var consulta = _context.Atendimentos
                .AsNoTracking()
                .Where(a => a.DataRegistro >= inicioUtc &&
                            a.DataRegistro < fimUtc &&
                            a.Veterinario!.Usuario!.ClinicaId == clinicaId);

            // RN-008: o veterinário só enxerga os próprios atendimentos.
            if (veterinarioId.HasValue)
            {
                consulta = consulta.Where(a => a.VeterinarioId == veterinarioId.Value);
            }

            return consulta;
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
