using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAtendimentoRepository
    {
        Task Adicionar(AtendimentoFisioterapeutico atendimento);
        Task<AtendimentoFisioterapeutico?> ObterPorId(Guid id);
        Task<AtendimentoFisioterapeutico?> ObterPorSessao(Guid sessaoId);
        Task<List<AtendimentoFisioterapeutico>> ObterPorTratamento(Guid tratamentoId);
        void Atualizar(AtendimentoFisioterapeutico atendimento);
        Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);

        /// <summary>HU-017: base dos relatórios de produtividade por período.</summary>
        Task<List<AtendimentoFisioterapeutico>> ObterPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);

        Task SalvarAlteracoes();
    }
}
