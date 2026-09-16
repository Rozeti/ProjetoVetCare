using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class TratamentoRepository : ITratamentoRepository
    {
        private readonly AppDbContext _context;

        public TratamentoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(Tratamento tratamento)
        {
            await _context.Tratamentos.AddAsync(tratamento);
        }

        public async Task<Tratamento?> ObterPorId(Guid id)
        {
            return await _context.Tratamentos.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Tratamento?> ObterPorIdComRelacionamentos(Guid id)
        {
            return await _context.Tratamentos
                .Include(t => t.Paciente)
                    .ThenInclude(p => p!.Tutor)
                        .ThenInclude(tu => tu!.Usuario)
                .Include(t => t.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<Tratamento>> ObterPorPaciente(Guid pacienteId)
        {
            return await _context.Tratamentos
                .AsNoTracking()
                .Include(t => t.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .Where(t => t.PacienteId == pacienteId)
                .OrderByDescending(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task<List<Tratamento>> ObterPorVeterinario(Guid veterinarioId)
        {
            return await _context.Tratamentos
                .AsNoTracking()
                .Include(t => t.Paciente)
                    .ThenInclude(p => p!.Tutor)
                        .ThenInclude(tu => tu!.Usuario)
                .Where(t => t.VeterinarioId == veterinarioId)
                .OrderByDescending(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task<Tratamento?> ObterAtivoDoPaciente(Guid pacienteId)
        {
            return await _context.Tratamentos
                .Where(t => t.PacienteId == pacienteId && t.Status == "Em Andamento")
                .OrderByDescending(t => t.DataInicio)
                .FirstOrDefaultAsync();
        }

        public void Atualizar(Tratamento tratamento)
        {
            _context.Tratamentos.Update(tratamento);
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
