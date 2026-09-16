using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IBloqueioAgendaRepository
    {
        Task Adicionar(BloqueioAgenda bloqueio);
        Task<BloqueioAgenda?> ObterPorId(Guid id);
        Task<List<BloqueioAgenda>> ObterPorPeriodo(Guid veterinarioId, DateTime inicio, DateTime fim);

        /// <summary>Indica se o intervalo informado cai dentro de um bloqueio do veterinário.</summary>
        Task<BloqueioAgenda?> ObterConflito(Guid veterinarioId, DateTime inicio, DateTime fim);

        void Remover(BloqueioAgenda bloqueio);
        Task SalvarAlteracoes();
    }
}
