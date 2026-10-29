using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAvaliacaoRepository
    {
        Task Adicionar(AvaliacaoClinica avaliacao);

        /// <summary>Carrega também o paciente, que decide a quem o registro pertence, e os tratamentos originados.</summary>
        Task<AvaliacaoClinica?> ObterPorId(Guid id);

        /// <summary>Avaliações do animal, da mais recente para a mais antiga, com os tratamentos originados.</summary>
        Task<List<AvaliacaoClinica>> ObterPorPaciente(Guid pacienteId);

        void Atualizar(AvaliacaoClinica avaliacao);
        Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);
        Task SalvarAlteracoes();
    }
}
