using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAvaliacaoRepository
    {
        Task Adicionar(AvaliacaoClinica avaliacao);

        /// <summary>Carrega também o tratamento e o paciente, que decidem a quem o registro pertence.</summary>
        Task<AvaliacaoClinica?> ObterPorId(Guid id);

        void Atualizar(AvaliacaoClinica avaliacao);
        Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);
        Task SalvarAlteracoes();
    }
}
