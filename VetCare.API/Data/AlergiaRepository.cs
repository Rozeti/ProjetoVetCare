using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class AlergiaRepository : IAlergiaRepository
    {
        private readonly AppDbContext _context;

        public AlergiaRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(AlergiaCondicao alergia) => await _context.AlergiasCondicoes.AddAsync(alergia);

        public async Task<AlergiaCondicao?> ObterPorId(Guid id)
        {
            return await _context.AlergiasCondicoes
                .Include(a => a.Paciente)
                .Include(a => a.RegistradoPor)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<AlergiaCondicao>> ObterPorPaciente(Guid pacienteId, bool apenasAtivas)
        {
            var consulta = _context.AlergiasCondicoes
                .AsNoTracking()
                .Include(a => a.RegistradoPor)
                .Where(a => a.PacienteId == pacienteId);

            if (apenasAtivas)
            {
                consulta = consulta.Where(a => a.Ativa);
            }

            return await consulta.OrderByDescending(a => a.DataRegistro).ToListAsync();
        }

        public void Atualizar(AlergiaCondicao alergia) => _context.AlergiasCondicoes.Update(alergia);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
