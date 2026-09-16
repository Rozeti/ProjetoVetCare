using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class VeterinarioRepository : IVeterinarioRepository
    {
        private readonly AppDbContext _context;

        public VeterinarioRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(Veterinario veterinario)
        {
            await _context.Veterinarios.AddAsync(veterinario);
        }

        public async Task<Veterinario?> ObterPorId(Guid id)
        {
            return await _context.Veterinarios
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Veterinario?> ObterPorUsuarioId(Guid usuarioId)
        {
            return await _context.Veterinarios
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId);
        }

        public async Task<List<Veterinario>> Listar(Guid clinicaId)
        {
            return await _context.Veterinarios
                .AsNoTracking()
                .Include(v => v.Usuario)
                .Where(v => v.Usuario!.ClinicaId == clinicaId && v.Usuario!.Ativo)
                .OrderBy(v => v.Usuario!.Nome)
                .ToListAsync();
        }

        public async Task<bool> CrmvExiste(string crmv, Guid? ignorarVeterinarioId = null)
        {
            var normalizado = crmv.Trim().ToLowerInvariant();

            return await _context.Veterinarios.AnyAsync(v =>
                v.Crmv.ToLower() == normalizado &&
                (ignorarVeterinarioId == null || v.Id != ignorarVeterinarioId));
        }

        public void Atualizar(Veterinario veterinario)
        {
            _context.Veterinarios.Update(veterinario);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
