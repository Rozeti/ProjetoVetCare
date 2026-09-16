using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IUsuarioRepository
    {
        Task Adicionar(Usuario usuario);
        Task<bool> EmailExiste(string email, Guid? ignorarUsuarioId = null);
        Task<Usuario?> ObterPorEmail(string email);
        Task<Usuario?> ObterPorId(Guid id);
        Task<PaginaDe<Usuario>> Listar(Guid clinicaId, string? perfil, string? busca, bool? ativo, ParametrosPagina parametros);
        Task<List<Usuario>> ListarTodos(Guid clinicaId, string? perfil, bool? ativo);
        void Atualizar(Usuario usuario);
        Task SalvarAlteracoes();
    }
}
