using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    /// <summary>Recorte da carteira de um paciente.</summary>
    public sealed class FiltroDeVacinas
    {
        /// <summary>Vacina, Vermifugo, Antipulgas ou Outro.</summary>
        public string? Tipo { get; init; }

        /// <summary>Em dia, A vencer, Vencida, Dose única ou Concluída.</summary>
        public string? Situacao { get; init; }

        /// <summary>Trecho do nome do produto, fabricante ou lote.</summary>
        public string? Busca { get; init; }
    }

    /// <summary>
    /// Uma aplicação e o fato de a dose seguinte do mesmo produto já ter sido
    /// registrada. Sem isso, a dose anterior continuaria "vencida" para sempre
    /// depois de o reforço ser aplicado.
    /// </summary>
    public sealed record VacinaComSituacao(Vacina Vacina, bool DoseSeguinteAplicada);

    public interface IVacinaRepository
    {
        Task Adicionar(Vacina vacina);
        Task<Vacina?> ObterPorId(Guid id);

        /// <summary>Carteira completa, da aplicação mais recente para a mais antiga.</summary>
        Task<List<VacinaComSituacao>> ObterPorPaciente(Guid pacienteId);

        /// <summary>Carteira filtrada e paginada (RNF-004).</summary>
        Task<PaginaDe<VacinaComSituacao>> ListarPorPaciente(Guid pacienteId, FiltroDeVacinas filtro, ParametrosPagina parametros);

        /// <summary>
        /// Aplicação mais recente do mesmo produto antes da data informada, usada para
        /// conferir a sequência de um esquema com várias doses.
        /// </summary>
        Task<Vacina?> ObterUltimaAplicacaoDoProduto(Guid pacienteId, string nome, DateTime antesDe, Guid? ignorarId = null);

        /// <summary>Doses ainda pendentes que vencem dentro da janela, para o painel de prevenção.</summary>
        Task<List<VacinaComSituacao>> ObterVencendo(Guid clinicaId, int diasDeAntecedencia);

        /// <summary>Doses a vencer que ainda não geraram lembrete para o tutor.</summary>
        Task<List<Vacina>> ObterPendentesDeLembrete(DateTime limite);

        /// <summary>Quantas doses vencidas e ainda pendentes cada paciente tem.</summary>
        Task<Dictionary<Guid, int>> ContarVencidasPorPaciente(IEnumerable<Guid> pacienteIds);

        void Atualizar(Vacina vacina);
        void Remover(Vacina vacina);
        Task SalvarAlteracoes();
    }
}
