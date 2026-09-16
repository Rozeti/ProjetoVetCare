using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IMensagemRepository
    {
        Task Adicionar(Mensagem mensagem);

        /// <summary>Histórico completo entre dois usuários, em ordem cronológica (HU-014, CA-1).</summary>
        Task<List<Mensagem>> ObterConversa(Guid usuarioA, Guid usuarioB);

        /// <summary>Todas as mensagens em que o usuário participa, base da lista de conversas (HU-014, CA-2).</summary>
        Task<List<Mensagem>> ObterMensagensDoUsuario(Guid usuarioId);

        Task<int> ContarNaoLidas(Guid usuarioId);

        /// <summary>HU-014, CA-3: zera o contador ao abrir a conversa.</summary>
        Task<int> MarcarConversaComoLida(Guid destinatarioId, Guid remetenteId);

        Task SalvarAlteracoes();
    }
}
