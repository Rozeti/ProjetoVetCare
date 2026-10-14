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

        Task<int> ContarCriadosDesde(Guid usuarioId, DateTime desde);

        /// <summary>
        /// Gasta uma tentativa de código antes da conferência. Devolve false quando outro
        /// pedido gravou o contador ao mesmo tempo: só um dos dois pode seguir.
        /// </summary>
        Task<bool> ReservarTentativaDeCodigo(TokenRedefinicaoSenha token);

        void Atualizar(TokenRedefinicaoSenha token);
        Task SalvarAlteracoes();
    }
}
