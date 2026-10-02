using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    /// <summary>Recortes da listagem de pacientes; cada campo nulo significa "sem filtro".</summary>
    public sealed class FiltroDePacientes
    {
        public Guid? TutorId { get; init; }

        /// <summary>Só os pacientes sob responsabilidade deste veterinário.</summary>
        public Guid? VeterinarioResponsavelId { get; init; }

        /// <summary>Só os pacientes que ainda não têm veterinário designado.</summary>
        public bool ApenasSemResponsavel { get; init; }

        public string? Busca { get; init; }
        public bool? Ativo { get; init; }
    }

    public interface IPetRepository
    {
        Task Adicionar(Pet pet);
        Task<Pet?> ObterPorId(Guid id);
        Task<Pet?> ObterPorIdComTutor(Guid id);
        Task<PaginaDe<Pet>> Listar(Guid clinicaId, FiltroDePacientes filtro, ParametrosPagina parametros);
        void Atualizar(Pet pet);

        /// <summary>HU-003, CA-4: um paciente com registros clínicos não pode ser excluído.</summary>
        Task<bool> PossuiRegistrosClinicos(Guid petId);

        /// <summary>Exclusão definitiva, permitida apenas quando não há histórico clínico (RN-004).</summary>
        Task Remover(Pet pet);

        /// <summary>O microchip identifica o animal e não pode se repetir na mesma clínica.</summary>
        Task<bool> MicrochipEmUso(Guid clinicaId, string microchip, Guid? ignorarPetId = null);

        /// <summary>Pacientes ativos da clínica ou, quando informado, só os de um veterinário.</summary>
        Task<int> ContarAtivos(Guid clinicaId, Guid? veterinarioResponsavelId = null);

        Task SalvarAlteracoes();
    }
}
