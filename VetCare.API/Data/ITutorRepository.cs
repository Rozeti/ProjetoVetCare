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

        /// <summary>Tutores da clínica, com busca por nome, e-mail ou telefone.</summary>
        Task<PaginaDe<Tutor>> Listar(Guid clinicaId, string? busca, ParametrosPagina parametros);

        Task<List<Tutor>> ListarTodos(Guid clinicaId);

        /// <summary>Se outro tutor da clínica já usa o CPF (comparado só pelos dígitos).</summary>
        Task<bool> CpfEmUso(Guid clinicaId, string cpf, Guid? ignorarTutorId = null);
        void Atualizar(Tutor tutor);
        Task SalvarAlteracoes();
    }
}
