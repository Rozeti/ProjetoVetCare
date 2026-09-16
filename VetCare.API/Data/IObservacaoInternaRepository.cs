using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IObservacaoInternaRepository
    {
        Task Adicionar(ObservacaoInterna observacao);
        Task<ObservacaoInterna?> ObterPorId(Guid id);
        Task<List<ObservacaoInterna>> ObterPorProntuario(Guid prontuarioId);
        Task SalvarAlteracoes();
    }
}
