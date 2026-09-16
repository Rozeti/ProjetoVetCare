using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class NotificacaoRepository : INotificacaoRepository
    {
        private readonly AppDbContext _context;

        public NotificacaoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Adicionar(Notificacao notificacao)
        {
            await _context.Notificacoes.AddAsync(notificacao);
        }

        public async Task AdicionarVarias(IEnumerable<Notificacao> notificacoes)
        {
            await _context.Notificacoes.AddRangeAsync(notificacoes);
        }

        public async Task<Notificacao?> ObterPorId(Guid id)
        {
            return await _context.Notificacoes.FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<List<Notificacao>> ObterPorUsuario(Guid usuarioId, bool apenasNaoVisualizadas, int limite)
        {
            var consulta = _context.Notificacoes
                .AsNoTracking()
                .Where(n => n.UsuarioId == usuarioId);

            if (apenasNaoVisualizadas)
            {
                consulta = consulta.Where(n => !n.Visualizada);
            }

            return await consulta
                .OrderByDescending(n => n.DataCriacao)
                .Take(limite)
                .ToListAsync();
        }

        public async Task<int> ContarNaoVisualizadas(Guid usuarioId)
        {
            return await _context.Notificacoes.CountAsync(n => n.UsuarioId == usuarioId && !n.Visualizada);
        }

        public async Task<int> MarcarTodasComoVisualizadas(Guid usuarioId)
        {
            return await _context.Notificacoes
                .Where(n => n.UsuarioId == usuarioId && !n.Visualizada)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Visualizada, true));
        }

        public async Task SalvarAlteracoes()
        {
            await _context.SaveChangesAsync();
        }
    }
}
