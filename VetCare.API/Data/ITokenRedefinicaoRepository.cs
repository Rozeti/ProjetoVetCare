using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ITokenRedefinicaoRepository
    {
        Task Adicionar(TokenRedefinicaoSenha token);

        /// <summary>O pedido mais recente ainda em aberto do usuário, com o usuário carregado.</summary>
        Task<TokenRedefinicaoSenha?> ObterAbertoDoUsuario(Guid usuarioId);

        /// <summary>Invalida pedidos anteriores para que apenas o último código funcione.</summary>
        Task InvalidarAnteriores(Guid usuarioId);

        void Atualizar(TokenRedefinicaoSenha token);
        Task SalvarAlteracoes();
    }
}
