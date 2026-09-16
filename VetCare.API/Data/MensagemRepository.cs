using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class MensagemRepository : IMensagemRepository
    {
        private readonly AppDbContext _context;

        public MensagemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(Mensagem mensagem)
        {
            await _context.Mensagens.AddAsync(mensagem);
        }

        public async Task<List<Mensagem>> ObterConversa(Guid usuarioA, Guid usuarioB)
        {
            return await _context.Mensagens
                .AsNoTracking()
                .Include(m => m.Remetente)
                .Include(m => m.Paciente)
                .Where(m => (m.RemetenteId == usuarioA && m.DestinatarioId == usuarioB) ||
                            (m.RemetenteId == usuarioB && m.DestinatarioId == usuarioA))
                .OrderBy(m => m.DataEnvio)
                .ToListAsync();
        }

        public async Task<List<Mensagem>> ObterMensagensDoUsuario(Guid usuarioId)
        {
            return await _context.Mensagens
                .AsNoTracking()
                .Include(m => m.Remetente)
                .Include(m => m.Destinatario)
                .Where(m => m.RemetenteId == usuarioId || m.DestinatarioId == usuarioId)
                .OrderByDescending(m => m.DataEnvio)
                .ToListAsync();
        }

        public async Task<int> ContarNaoLidas(Guid usuarioId)
        {
            return await _context.Mensagens.CountAsync(m => m.DestinatarioId == usuarioId && !m.Lida);
        }

        public async Task<int> MarcarConversaComoLida(Guid destinatarioId, Guid remetenteId)
        {
            return await _context.Mensagens
                .Where(m => m.DestinatarioId == destinatarioId && m.RemetenteId == remetenteId && !m.Lida)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Lida, true));
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
