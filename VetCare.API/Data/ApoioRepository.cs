using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class ApoioRepository : IApoioRepository
    {
        private readonly AppDbContext _context;

        public ApoioRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(ApoioAdministrativo apoio)
        {
            await _context.ApoiosAdministrativos.AddAsync(apoio);
        }

        public async Task<ApoioAdministrativo?> ObterPorUsuarioId(Guid usuarioId)
        {
            return await _context.ApoiosAdministrativos
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
