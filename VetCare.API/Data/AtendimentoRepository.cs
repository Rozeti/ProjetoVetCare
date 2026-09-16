using Microsoft.EntityFrameworkCore;
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
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<AtendimentoFisioterapeutico?> ObterPorSessao(Guid sessaoId)
        {
            return await _context.Atendimentos.FirstOrDefaultAsync(a => a.SessaoId == sessaoId);
        }

        public async Task<List<AtendimentoFisioterapeutico>> ObterPorTratamento(Guid tratamentoId)
        {
            return await _context.Atendimentos
                .AsNoTracking()
                .Where(a => a.TratamentoId == tratamentoId)
                .OrderByDescending(a => a.DataRegistro)
                .ToListAsync();
        }

        public void Atualizar(AtendimentoFisioterapeutico atendimento)
        {
            _context.Atendimentos.Update(atendimento);
        }

        public async Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim)
        {
            return await MontarConsultaPorPeriodo(clinicaId, veterinarioId, inicio, fim).CountAsync();
        }

        public async Task<List<AtendimentoFisioterapeutico>> ObterPorPeriodo(
            Guid clinicaId,
            Guid? veterinarioId,
            DateTime inicio,
            DateTime fim)
        {
            return await MontarConsultaPorPeriodo(clinicaId, veterinarioId, inicio, fim)
                .Include(a => a.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .OrderBy(a => a.DataRegistro)
                .ToListAsync();
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
