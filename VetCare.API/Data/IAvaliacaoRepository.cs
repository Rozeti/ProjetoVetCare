using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAvaliacaoRepository
    {
        Task Adicionar(AvaliacaoClinica avaliacao);
        Task<AvaliacaoClinica?> ObterPorId(Guid id);
        Task<List<AvaliacaoClinica>> ObterPorTratamento(Guid tratamentoId);
        void Atualizar(AvaliacaoClinica avaliacao);
        Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);
        Task SalvarAlteracoes();
    }
}
