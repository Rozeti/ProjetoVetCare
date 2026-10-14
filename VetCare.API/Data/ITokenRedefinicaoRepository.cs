using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ITokenRedefinicaoRepository
    {
        Task Adicionar(TokenRedefinicaoSenha token);
        Task<TokenRedefinicaoSenha?> ObterPorHash(string tokenHash);

        /// <summary>O pedido mais recente ainda em aberto do usuário, com o usuário carregado.</summary>
        Task<TokenRedefinicaoSenha?> ObterAbertoDoUsuario(Guid usuarioId);

        /// <summary>Invalida pedidos anteriores para que apenas o último link e código funcionem.</summary>
        Task InvalidarAnteriores(Guid usuarioId);

        void Atualizar(TokenRedefinicaoSenha token);
        Task SalvarAlteracoes();
    }
}
