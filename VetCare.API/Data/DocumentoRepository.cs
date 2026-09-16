using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class DocumentoRepository : IDocumentoRepository
    {
        private readonly AppDbContext _context;

        public DocumentoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(DocumentoClinico documento)
        {
            await _context.DocumentosClinicos.AddAsync(documento);
        }

        public async Task<DocumentoClinico?> ObterPorId(Guid id)
        {
            return await _context.DocumentosClinicos
                .Include(d => d.EnviadoPor)
                .Include(d => d.Prontuario)
                    .ThenInclude(p => p!.Paciente)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<DocumentoClinico>> ObterPorProntuario(Guid prontuarioId)
        {
            return await _context.DocumentosClinicos
                .AsNoTracking()
                .Include(d => d.EnviadoPor)
                .Where(d => d.ProntuarioId == prontuarioId)
                .OrderByDescending(d => d.DataUpload)
                .ToListAsync();
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
