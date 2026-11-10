using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class SessaoRepository : ISessaoRepository
    {
        private readonly AppDbContext _context;

        public SessaoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(Sessao sessao)
        {
            await _context.Sessoes.AddAsync(sessao);
        }

        public async Task<bool> ExisteConflitoHorario(
            Guid veterinarioId,
            DateTime inicio,
            DateTime fim,
            Guid? ignorarSessaoId = null)
        {
            var inicioNovo = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimNovo = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            // Duas sessões colidem quando uma começa antes de a outra terminar. Sessões
            // canceladas (HU-006, CA-2) e inativadas liberam o horário.
            return await _context.Sessoes.AnyAsync(s =>
                s.VeterinarioId == veterinarioId &&
                !s.Inativa &&
                s.Status != StatusSessao.Cancelada &&
                (ignorarSessaoId == null || s.Id != ignorarSessaoId) &&
                s.DataHora < fimNovo &&
                inicioNovo < s.DataHoraFim);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<Sessao?> ObterPorId(Guid id)
        {
            return await _context.Sessoes.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Sessao?> ObterPorIdComRelacionamentos(Guid id)
        {
            return await ComRelacionamentos(_context.Sessoes).FirstOrDefaultAsync(s => s.Id == id);
        }

        public void Atualizar(Sessao sessao)
        {
            _context.Sessoes.Update(sessao);
        }

        public async Task<List<Sessao>> ObterPorPeriodo(Guid veterinarioId, DateTime inicio, DateTime fim)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            return await ComRelacionamentos(_context.Sessoes.AsNoTracking())
                .Where(s => s.VeterinarioId == veterinarioId &&
                            !s.Inativa &&
                            s.DataHora >= inicioUtc &&
                            s.DataHora < fimUtc)
                .OrderBy(s => s.DataHora)
                .ToListAsync();
        }

        public async Task<List<Sessao>> ObterAgendaGeral(
            Guid clinicaId,
            DateTime inicio,
            DateTime fim,
            List<Guid>? veterinariosFiltrados)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            var consulta = ComRelacionamentos(_context.Sessoes.AsNoTracking())
                .Where(s => !s.Inativa &&
                            s.DataHora >= inicioUtc &&
                            s.DataHora < fimUtc &&
                            s.Veterinario!.Usuario!.ClinicaId == clinicaId);

            // HU-005, CA-2: toggle que exibe ou oculta veterinários específicos.
            if (veterinariosFiltrados != null && veterinariosFiltrados.Count > 0)
            {
                consulta = consulta.Where(s => veterinariosFiltrados.Contains(s.VeterinarioId));
            }

            return await consulta.OrderBy(s => s.DataHora).ToListAsync();
        }

        public async Task<List<Sessao>> ObterPorTutor(Guid tutorId, bool apenasFuturas)
        {
            var agora = DateTime.UtcNow;

            var consulta = _context.Sessoes
                .AsNoTracking()
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(s => s.Tratamento!.Paciente!.TutorId == tutorId && !s.Inativa);

            if (apenasFuturas)
            {
                consulta = consulta.Where(s => s.DataHora >= agora && s.Status != StatusSessao.Cancelada);
            }

            return await consulta.OrderBy(s => s.DataHora).ToListAsync();
        }

        public async Task<List<Sessao>> ObterPorTratamento(Guid tratamentoId, bool incluirInativas)
        {
            return await _context.Sessoes
                .AsNoTracking()
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(s => s.TratamentoId == tratamentoId && (incluirInativas || !s.Inativa))
                .OrderBy(s => s.DataHora)
                .ToListAsync();
        }

        public async Task<List<Sessao>> ObterPorPaciente(Guid pacienteId, bool incluirInativas)
        {
            return await _context.Sessoes
                .AsNoTracking()
                .Include(s => s.Tratamento)
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(s => s.Tratamento!.PacienteId == pacienteId && (incluirInativas || !s.Inativa))
                .OrderBy(s => s.DataHora)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, ContagemDeSessoes>> ContarPorTratamentos(IEnumerable<Guid> tratamentosIds)
        {
            var ids = tratamentosIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<Guid, ContagemDeSessoes>();
            }

            // As canceladas e as inativadas não fazem parte do plano do tratamento.
            var sessoes = await _context.Sessoes
                .AsNoTracking()
                .Where(s => ids.Contains(s.TratamentoId) && !s.Inativa && s.Status != StatusSessao.Cancelada)
                .Select(s => new { s.TratamentoId, s.Status })
                .ToListAsync();

            return sessoes
                .GroupBy(s => s.TratamentoId)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => new ContagemDeSessoes(grupo.Count(), grupo.Count(s => s.Status == StatusSessao.Concluida)));
        }

        public async Task<List<Sessao>> ObterPendentesDeLembrete(DateTime limite)
        {
            var agora = DateTime.UtcNow;
            var limiteUtc = DateTime.SpecifyKind(limite, DateTimeKind.Utc);

            // Paciente inativo ou falecido não recebe mais lembrete, mesmo que a sessão
            // antiga ainda esteja na agenda.
            return await _context.Sessoes
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                        .ThenInclude(p => p!.Tutor)
                .Where(s => !s.LembreteEnviado &&
                            !s.Inativa &&
                            s.Status == StatusSessao.AguardandoConfirmacao &&
                            s.DataHora > agora &&
                            s.DataHora <= limiteUtc &&
                            s.Tratamento!.Paciente!.Ativo &&
                            s.Tratamento!.Paciente!.DataObito == null)
                .ToListAsync();
        }

        public async Task<PaginaDe<Sessao>> Listar(
            Guid clinicaId,
            FiltroDeSessoes filtro,
            string? situacao,
            DateTime agoraUtc,
            ParametrosPagina parametros)
        {
            var consulta = NaSituacao(Filtrar(ComRelacionamentos(_context.Sessoes.AsNoTracking()), clinicaId, filtro), situacao, agoraUtc);

            // As próximas sessões vêm na ordem em que vão acontecer; as demais, da mais recente para a mais antiga.
            consulta = situacao == SituacaoSessao.Agendada
                ? consulta.OrderBy(s => s.DataHora)
                : consulta.OrderByDescending(s => s.DataHora);

            return await consulta.Paginar(parametros);
        }

        public async Task<Dictionary<string, int>> ContarPorSituacao(Guid clinicaId, FiltroDeSessoes filtro, DateTime agoraUtc)
        {
            var filtradas = Filtrar(_context.Sessoes.AsNoTracking(), clinicaId, filtro);
            var contagens = new Dictionary<string, int>();

            foreach (var situacao in SituacaoSessao.Todas)
            {
                contagens[situacao] = await NaSituacao(filtradas, situacao, agoraUtc).CountAsync();
            }

            return contagens;
        }

        private IQueryable<Sessao> NaSituacao(IQueryable<Sessao> consulta, string? situacao, DateTime agoraUtc)
        {
            var atendimentos = _context.Atendimentos;

            return situacao switch
            {
                SituacaoSessao.Inativada => consulta.Where(s => s.Inativa),
                SituacaoSessao.Cancelada => consulta.Where(s => !s.Inativa && s.Status == StatusSessao.Cancelada),
                SituacaoSessao.Realizada => consulta.Where(s =>
                    !s.Inativa && s.Status != StatusSessao.Cancelada &&
                    (s.Status == StatusSessao.Concluida || atendimentos.Any(a => a.SessaoId == s.Id))),
                SituacaoSessao.Agendada => consulta.Where(s =>
                    !s.Inativa && s.Status != StatusSessao.Cancelada && s.Status != StatusSessao.Concluida &&
                    !atendimentos.Any(a => a.SessaoId == s.Id) && s.DataHora > agoraUtc),
                SituacaoSessao.AguardandoRegistro => consulta.Where(s =>
                    !s.Inativa && s.Status != StatusSessao.Cancelada && s.Status != StatusSessao.Concluida &&
                    !atendimentos.Any(a => a.SessaoId == s.Id) && s.DataHora <= agoraUtc),
                _ => consulta.Where(s => !s.Inativa)
            };
        }

        private static IQueryable<Sessao> Filtrar(IQueryable<Sessao> consulta, Guid clinicaId, FiltroDeSessoes filtro)
        {
            consulta = consulta.Where(s => s.Tratamento!.Paciente!.ClinicaId == clinicaId);

            if (filtro.PacienteId.HasValue)
                consulta = consulta.Where(s => s.Tratamento!.PacienteId == filtro.PacienteId);

            if (filtro.TratamentoId.HasValue)
                consulta = consulta.Where(s => s.TratamentoId == filtro.TratamentoId);

            if (filtro.VeterinarioId.HasValue)
                consulta = consulta.Where(s => s.VeterinarioId == filtro.VeterinarioId);

            if (filtro.InicioUtc.HasValue)
                consulta = consulta.Where(s => s.DataHora >= filtro.InicioUtc);

            if (filtro.FimUtc.HasValue)
                consulta = consulta.Where(s => s.DataHora < filtro.FimUtc);

            return consulta;
        }

        private static IQueryable<Sessao> ComRelacionamentos(IQueryable<Sessao> consulta)
        {
            return consulta
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                        .ThenInclude(p => p!.Tutor)
                            .ThenInclude(t => t!.Usuario)
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario);
        }
    }
}
