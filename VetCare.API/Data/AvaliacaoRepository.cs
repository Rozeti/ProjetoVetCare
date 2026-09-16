using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class AvaliacaoRepository : IAvaliacaoRepository
    {
        private readonly AppDbContext _context;

        public AvaliacaoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(AvaliacaoClinica avaliacao)
        {
            await _context.AvaliacoesClinicas.AddAsync(avaliacao);
        }

        public async Task<AvaliacaoClinica?> ObterPorId(Guid id)
        {
            return await _context.AvaliacoesClinicas
                .Include(a => a.Veterinario)
                    .ThenInclude(v => v!.Usuario)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<AvaliacaoClinica>> ObterPorTratamento(Guid tratamentoId)
        {
            return await _context.AvaliacoesClinicas
                .AsNoTracking()
                .Where(a => a.TratamentoId == tratamentoId)
                .OrderByDescending(a => a.DataRegistro)
                .ToListAsync();
        }

        public void Atualizar(AvaliacaoClinica avaliacao)
        {
            _context.AvaliacoesClinicas.Update(avaliacao);
        }

        public async Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim)
        {
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim, DateTimeKind.Utc);

            var consulta = _context.AvaliacoesClinicas
                .Where(a => a.DataRegistro >= inicioUtc &&
                            a.DataRegistro < fimUtc &&
                            a.Veterinario!.Usuario!.ClinicaId == clinicaId);

            // RN-008: o veterinário só enxerga os próprios registros.
            if (veterinarioId.HasValue)
            {
                consulta = consulta.Where(a => a.VeterinarioId == veterinarioId.Value);
            }

            return await consulta.CountAsync();
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
