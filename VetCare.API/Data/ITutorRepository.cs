using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ITutorRepository
    {
        Task Adicionar(Tutor tutor);
        Task<Tutor?> ObterPorId(Guid id);
        Task<Tutor?> ObterPorUsuarioId(Guid usuarioId);
        Task<PaginaDe<Tutor>> Listar(Guid clinicaId, string? busca, ParametrosPagina parametros);
        Task<List<Tutor>> ListarTodos(Guid clinicaId);
        void Atualizar(Tutor tutor);
        Task SalvarAlteracoes();
    }
}
