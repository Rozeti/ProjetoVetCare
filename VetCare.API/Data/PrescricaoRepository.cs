using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class PrescricaoRepository : IPrescricaoRepository
    {
        private readonly AppDbContext _context;

        public PrescricaoRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(Prescricao prescricao) => await _context.Prescricoes.AddAsync(prescricao);

        public async Task<Prescricao?> ObterPorId(Guid id)
        {
            return await _context.Prescricoes
                .Include(p => p.Itens)
                .Include(p => p.Paciente).ThenInclude(pa => pa!.Tutor).ThenInclude(t => t!.Usuario)
                .Include(p => p.Veterinario).ThenInclude(v => v!.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Prescricao>> ObterPorProntuario(Guid prontuarioId)
        {
            return await _context.Prescricoes
                .AsNoTracking()
                .Include(p => p.Itens)
                .Include(p => p.Veterinario).ThenInclude(v => v!.Usuario)
                .Where(p => p.ProntuarioId == prontuarioId)
                .OrderByDescending(p => p.DataEmissao)
                .ToListAsync();
        }

        public void Atualizar(Prescricao prescricao) => _context.Prescricoes.Update(prescricao);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
