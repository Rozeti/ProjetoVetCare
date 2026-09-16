using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class BloqueioAgendaRepository : IBloqueioAgendaRepository
    {
        private readonly AppDbContext _context;

        public BloqueioAgendaRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(BloqueioAgenda bloqueio) => await _context.BloqueiosAgenda.AddAsync(bloqueio);

        public async Task<BloqueioAgenda?> ObterPorId(Guid id)
        {
            return await _context.BloqueiosAgenda
                .Include(b => b.Veterinario).ThenInclude(v => v!.Usuario)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<List<BloqueioAgenda>> ObterPorPeriodo(Guid veterinarioId, DateTime inicio, DateTime fim)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            return await _context.BloqueiosAgenda
                .AsNoTracking()
                .Where(b => b.VeterinarioId == veterinarioId && b.Inicio < fimUtc && inicioUtc < b.Fim)
                .OrderBy(b => b.Inicio)
                .ToListAsync();
        }

        public async Task<BloqueioAgenda?> ObterConflito(Guid veterinarioId, DateTime inicio, DateTime fim)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            return await _context.BloqueiosAgenda
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.VeterinarioId == veterinarioId && b.Inicio < fimUtc && inicioUtc < b.Fim);
        }

        public void Remover(BloqueioAgenda bloqueio) => _context.BloqueiosAgenda.Remove(bloqueio);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
