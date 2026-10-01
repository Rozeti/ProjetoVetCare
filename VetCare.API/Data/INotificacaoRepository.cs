using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface INotificacaoRepository
    {
        Task Adicionar(Notificacao notificacao);
        Task<Notificacao?> ObterPorId(Guid id);
        Task<List<Notificacao>> ObterPorUsuario(Guid usuarioId, bool apenasNaoVisualizadas, int limite);
        Task<int> ContarNaoVisualizadas(Guid usuarioId);
        Task<int> MarcarTodasComoVisualizadas(Guid usuarioId);

        /// <summary>
        /// Notificações que ainda precisam ser enviadas por e-mail ou push e cujo prazo de
        /// nova tentativa já passou, com o usuário carregado para decidir os canais.
        /// </summary>
        Task<List<Notificacao>> ObterPendentesDeEntrega(DateTime agora, int limite);

        Task SalvarAlteracoes();
    }
}
