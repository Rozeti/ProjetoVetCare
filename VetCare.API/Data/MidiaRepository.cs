using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class MidiaRepository : IMidiaRepository
    {
        private readonly AppDbContext _context;

        public MidiaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(MidiaSessao midia)
        {
            await _context.MidiasSessao.AddAsync(midia);
        }

        public async Task<MidiaSessao?> ObterPorId(Guid id)
        {
            return await _context.MidiasSessao.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<MidiaSessao>> ObterPorSessao(Guid sessaoId)
        {
            return await _context.MidiasSessao
                .AsNoTracking()
                .Where(m => m.SessaoId == sessaoId)
                .OrderByDescending(m => m.DataUpload)
                .ToListAsync();
        }

        public void Remover(MidiaSessao midia)
        {
            _context.MidiasSessao.Remove(midia);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
