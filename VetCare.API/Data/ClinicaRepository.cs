using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class ClinicaRepository : IClinicaRepository
    {
        private readonly AppDbContext _context;

        public ClinicaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Clinica?> ObterPorId(Guid id)
        {
            return await _context.Clinicas.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Clinica?> ObterPrimeira()
        {
            return await _context.Clinicas.OrderBy(c => c.DataCadastro).FirstOrDefaultAsync();
        }

        public void Atualizar(Clinica clinica)
        {
            _context.Clinicas.Update(clinica);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
