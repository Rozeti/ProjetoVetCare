using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ITutorRepository
    {
        Task Adicionar(Tutor tutor);
        Task<Tutor?> ObterPorId(Guid id);
        Task<Tutor?> ObterPorUsuarioId(Guid usuarioId);

        /// <summary>Vínculos de vários usuários numa só consulta, para as listagens paginadas.</summary>
        Task<List<Tutor>> ObterPorUsuarios(IEnumerable<Guid> usuariosIds);

        /// <summary>
        /// Com <paramref name="veterinarioId"/>, devolve só os tutores ligados a ele: os que
        /// têm algum paciente sob sua responsabilidade e os que ainda não têm paciente.
        /// </summary>
        Task<PaginaDe<Tutor>> Listar(Guid clinicaId, string? busca, ParametrosPagina parametros, Guid? veterinarioId = null);

        Task<List<Tutor>> ListarTodos(Guid clinicaId, Guid? veterinarioId = null);
        void Atualizar(Tutor tutor);
        Task SalvarAlteracoes();
    }
}
