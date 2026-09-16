using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class VersaoRegistroRepository : IVersaoRegistroRepository
    {
        private static readonly JsonSerializerOptions OpcoesJson = new()
        {
            WriteIndented = false,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        private readonly AppDbContext _context;

        public VersaoRegistroRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Arquivar(string tipoRegistro, Guid registroId, object estadoAnterior, Guid alteradoPorId)
        {
            var versao = new VersaoRegistroClinico
            {
                TipoRegistro = tipoRegistro,
                RegistroId = registroId,
                ConteudoAnterior = JsonSerializer.Serialize(estadoAnterior, OpcoesJson),
                AlteradoPorId = alteradoPorId
            };

            await _context.VersoesRegistrosClinicos.AddAsync(versao);
        }

        public async Task<List<VersaoRegistroClinico>> ObterHistorico(string tipoRegistro, Guid registroId)
        {
            return await _context.VersoesRegistrosClinicos
                .AsNoTracking()
                .Include(v => v.AlteradoPor)
                .Where(v => v.TipoRegistro == tipoRegistro && v.RegistroId == registroId)
                .OrderByDescending(v => v.DataAlteracao)
                .ToListAsync();
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
