using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class ObservacaoInternaRepository : IObservacaoInternaRepository
    {
        private readonly AppDbContext _context;

        public ObservacaoInternaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(ObservacaoInterna observacao)
        {
            await _context.ObservacoesInternas.AddAsync(observacao);
        }

        public async Task<ObservacaoInterna?> ObterPorId(Guid id)
        {
            return await _context.ObservacoesInternas
                .Include(o => o.Autor)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<List<ObservacaoInterna>> ObterPorProntuario(Guid prontuarioId)
        {
            return await _context.ObservacoesInternas
                .AsNoTracking()
                .Include(o => o.Autor)
                .Where(o => o.ProntuarioId == prontuarioId)
                .OrderByDescending(o => o.DataRegistro)
                .ToListAsync();
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
