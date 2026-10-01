using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAtendimentoRepository
    {
        Task Adicionar(AtendimentoFisioterapeutico atendimento);

        /// <summary>Carrega também a sessão, o tratamento e o paciente, que decidem a quem o registro pertence.</summary>
        Task<AtendimentoFisioterapeutico?> ObterPorId(Guid id);

        Task<AtendimentoFisioterapeutico?> ObterPorSessao(Guid sessaoId);

        /// <summary>Quais das sessões informadas já têm atendimento, numa só consulta.</summary>
        Task<HashSet<Guid>> ObterSessoesComAtendimento(IEnumerable<Guid> sessoesIds);

        void Atualizar(AtendimentoFisioterapeutico atendimento);
        Task<int> ContarPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);

        /// <summary>HU-017: base dos relatórios de produtividade por período.</summary>
        Task<List<AtendimentoFisioterapeutico>> ObterPorPeriodo(Guid clinicaId, Guid? veterinarioId, DateTime inicio, DateTime fim);

        Task SalvarAlteracoes();
    }
}
