using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ITokenRedefinicaoRepository
    {
        Task Adicionar(TokenRedefinicaoSenha token);
        Task<TokenRedefinicaoSenha?> ObterPorHash(string tokenHash);

        /// <summary>Invalida pedidos anteriores para que apenas o último link funcione.</summary>
        Task InvalidarAnteriores(Guid usuarioId);

        Task SalvarAlteracoes();
    }
}
