using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class VacinaRepository : IVacinaRepository
    {
        private readonly AppDbContext _context;

        public VacinaRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(Vacina vacina) => await _context.Vacinas.AddAsync(vacina);

        public async Task<Vacina?> ObterPorId(Guid id)
        {
            return await _context.Vacinas
                .Include(v => v.Paciente)
                .Include(v => v.Veterinario).ThenInclude(vet => vet!.Usuario)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<List<Vacina>> ObterPorPaciente(Guid pacienteId)
        {
            return await _context.Vacinas
                .AsNoTracking()
                .Include(v => v.Veterinario).ThenInclude(vet => vet!.Usuario)
                .Where(v => v.PacienteId == pacienteId)
                .OrderByDescending(v => v.DataAplicacao)
                .ToListAsync();
        }

        public async Task<List<Vacina>> ObterVencendo(Guid clinicaId, int diasDeAntecedencia)
        {
            var limite = DateTime.UtcNow.Date.AddDays(diasDeAntecedencia);

            return await _context.Vacinas
                .AsNoTracking()
                .Include(v => v.Paciente).ThenInclude(p => p!.Tutor).ThenInclude(t => t!.Usuario)
                .Where(v => v.ProximaDose != null &&
                            v.ProximaDose <= limite &&
                            v.Paciente!.ClinicaId == clinicaId &&
                            v.Paciente.Ativo)
                .OrderBy(v => v.ProximaDose)
                .ToListAsync();
        }

        public async Task<List<Vacina>> ObterPendentesDeLembrete(DateTime limite)
        {
            return await _context.Vacinas
                .Include(v => v.Paciente).ThenInclude(p => p!.Tutor)
                .Where(v => !v.LembreteEnviado &&
                            v.ProximaDose != null &&
                            v.ProximaDose <= limite &&
                            v.Paciente!.Ativo)
                .ToListAsync();
        }

        public void Atualizar(Vacina vacina) => _context.Vacinas.Update(vacina);

        public void Remover(Vacina vacina) => _context.Vacinas.Remove(vacina);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
