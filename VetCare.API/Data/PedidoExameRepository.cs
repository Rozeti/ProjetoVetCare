using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class PedidoExameRepository : IPedidoExameRepository
    {
        private readonly AppDbContext _context;

        public PedidoExameRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(PedidoExame pedido) => await _context.PedidosExame.AddAsync(pedido);

        public async Task<PedidoExame?> ObterPorId(Guid id)
        {
            return await _context.PedidosExame
                .Include(p => p.Paciente).ThenInclude(pa => pa!.Tutor).ThenInclude(t => t!.Usuario)
                .Include(p => p.Veterinario).ThenInclude(v => v!.Usuario)
                .Include(p => p.Avaliacao)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<PedidoExame>> ObterPorProntuario(Guid prontuarioId)
        {
            return await _context.PedidosExame
                .AsNoTracking()
                .Include(p => p.Veterinario).ThenInclude(v => v!.Usuario)
                .Include(p => p.Avaliacao)
                .Where(p => p.ProntuarioId == prontuarioId)
                .OrderByDescending(p => p.DataEmissao)
                .ToListAsync();
        }

        public void Atualizar(PedidoExame pedido) => _context.PedidosExame.Update(pedido);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
