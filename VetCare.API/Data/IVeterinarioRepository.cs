using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IVeterinarioRepository
    {
        Task Adicionar(Veterinario veterinario);
        Task<Veterinario?> ObterPorId(Guid id);
        Task<Veterinario?> ObterPorUsuarioId(Guid usuarioId);

        /// <summary>Vínculos de vários usuários numa só consulta, para as listagens paginadas.</summary>
        Task<List<Veterinario>> ObterPorUsuarios(IEnumerable<Guid> usuariosIds);

        Task<List<Veterinario>> Listar(Guid clinicaId);
        Task<bool> CrmvExiste(string crmv, Guid? ignorarVeterinarioId = null);
        void Atualizar(Veterinario veterinario);
        Task SalvarAlteracoes();
    }
}
