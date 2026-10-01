using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IDispositivoRepository
    {
        Task<DispositivoDoUsuario?> ObterPorToken(string tokenPush);
        Task<List<DispositivoDoUsuario>> ObterAtivosDoUsuario(Guid usuarioId);

        /// <summary>Aparelhos ativos de vários usuários de uma vez, para a entrega em lote.</summary>
        Task<List<DispositivoDoUsuario>> ObterAtivosDosUsuarios(IEnumerable<Guid> usuariosIds);

        Task Adicionar(DispositivoDoUsuario dispositivo);
        void Atualizar(DispositivoDoUsuario dispositivo);
        Task SalvarAlteracoes();
    }
}
