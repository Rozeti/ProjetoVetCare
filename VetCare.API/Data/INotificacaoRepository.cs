using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface INotificacaoRepository
    {
        Task Adicionar(Notificacao notificacao);
        Task AdicionarVarias(IEnumerable<Notificacao> notificacoes);
        Task<Notificacao?> ObterPorId(Guid id);
        Task<List<Notificacao>> ObterPorUsuario(Guid usuarioId, bool apenasNaoVisualizadas, int limite);
        Task<int> ContarNaoVisualizadas(Guid usuarioId);
        Task<int> MarcarTodasComoVisualizadas(Guid usuarioId);
        Task SalvarAlteracoes();
    }
}
