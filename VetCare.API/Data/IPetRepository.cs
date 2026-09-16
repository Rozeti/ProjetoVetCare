using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IPetRepository
    {
        Task Adicionar(Pet pet);
        Task<Pet?> ObterPorId(Guid id);
        Task<Pet?> ObterPorIdComTutor(Guid id);
        Task<PaginaDe<Pet>> Listar(Guid clinicaId, Guid? tutorId, string? busca, bool? ativo, ParametrosPagina parametros);
        Task<List<Pet>> ObterPorTutor(Guid tutorId);
        void Atualizar(Pet pet);

        /// <summary>HU-003, CA-4: um paciente com registros clínicos não pode ser excluído.</summary>
        Task<bool> PossuiRegistrosClinicos(Guid petId);

        /// <summary>O microchip identifica o animal e não pode se repetir na mesma clínica.</summary>
        Task<bool> MicrochipEmUso(Guid clinicaId, string microchip, Guid? ignorarPetId = null);

        Task<int> ContarAtivos(Guid clinicaId);
        Task SalvarAlteracoes();
    }
}
