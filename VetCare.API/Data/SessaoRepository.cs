using Microsoft.EntityFrameworkCore;
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
            DateTime dataHora,
            int duracaoMinutos,
            Guid? ignorarSessaoId = null)
        {
            var inicioNovo = DateTime.SpecifyKind(dataHora, DateTimeKind.Utc);
            var fimNovo = inicioNovo.AddMinutes(duracaoMinutos);

            // Duas sessões colidem quando uma começa antes de a outra terminar.
            // Sessões canceladas liberam o horário (HU-006, CA-2).
            return await _context.Sessoes.AnyAsync(s =>
                s.VeterinarioId == veterinarioId &&
                s.Status != "Cancelada" &&
                (ignorarSessaoId == null || s.Id != ignorarSessaoId) &&
                s.DataHora < fimNovo &&
                inicioNovo < s.DataHora.AddMinutes(duracaoMinutos));
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
            return await _context.Sessoes
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                        .ThenInclude(p => p!.Tutor)
                            .ThenInclude(t => t!.Usuario)
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public void Atualizar(Sessao sessao)
        {
            _context.Sessoes.Update(sessao);
        }

        public Task<List<Sessao>> ObterSessoesDoDia(Guid veterinarioId, DateTime data)
        {
            var inicioDoDia = DateTime.SpecifyKind(data.Date, DateTimeKind.Utc);
            return ObterPorPeriodo(veterinarioId, inicioDoDia, inicioDoDia.AddDays(1));
        }

        public async Task<List<Sessao>> ObterPorPeriodo(Guid veterinarioId, DateTime inicio, DateTime fim)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            return await _context.Sessoes
                .AsNoTracking()
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                        .ThenInclude(p => p!.Tutor)
                            .ThenInclude(t => t!.Usuario)
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(s => s.VeterinarioId == veterinarioId &&
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

            var consulta = _context.Sessoes
                .AsNoTracking()
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                        .ThenInclude(p => p!.Tutor)
                            .ThenInclude(t => t!.Usuario)
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(s => s.DataHora >= inicioUtc &&
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
                .Where(s => s.Tratamento!.Paciente!.TutorId == tutorId);

            if (apenasFuturas)
            {
                consulta = consulta.Where(s => s.DataHora >= agora && s.Status != "Cancelada");
            }

            return await consulta.OrderBy(s => s.DataHora).ToListAsync();
        }

        public async Task<List<Sessao>> ObterPorTratamento(Guid tratamentoId)
        {
            return await _context.Sessoes
                .AsNoTracking()
                .Include(s => s.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(s => s.TratamentoId == tratamentoId)
                .OrderBy(s => s.DataHora)
                .ToListAsync();
        }

        public async Task<List<Sessao>> ObterPendentesDeLembrete(DateTime limite)
        {
            var agora = DateTime.UtcNow;
            var limiteUtc = DateTime.SpecifyKind(limite, DateTimeKind.Utc);

            return await _context.Sessoes
                .Include(s => s.Tratamento)
                    .ThenInclude(t => t!.Paciente)
                        .ThenInclude(p => p!.Tutor)
                .Where(s => !s.LembreteEnviado &&
                            s.Status == "Aguardando confirmação" &&
                            s.DataHora > agora &&
                            s.DataHora <= limiteUtc)
                .ToListAsync();
        }
    }
}
