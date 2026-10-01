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

        /// <summary>
        /// Renova o último acesso do usuário, mas só quando o registro anterior já tem
        /// mais de <paramref name="intervaloMinimo"/>: é o que mantém o indicador "online"
        /// correto sem transformar cada consulta numa escrita no banco.
        /// </summary>
        Task RegistrarAtividade(Guid usuarioId, TimeSpan intervaloMinimo);

        Task SalvarAlteracoes();
    }
}
