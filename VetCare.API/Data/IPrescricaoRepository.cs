using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IPrescricaoRepository
    {
        Task Adicionar(Prescricao prescricao);
        Task<Prescricao?> ObterPorId(Guid id);
        Task<List<Prescricao>> ObterPorProntuario(Guid prontuarioId);
        void Atualizar(Prescricao prescricao);
        Task SalvarAlteracoes();
    }
}
