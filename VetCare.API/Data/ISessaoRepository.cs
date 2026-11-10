using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    /// <summary>Quantas sessões ativas e não canceladas um tratamento tem e quantas já foram concluídas.</summary>
    public sealed record ContagemDeSessoes(int Total, int Concluidas);

    /// <summary>Filtros da tela de gestão de sessões; as datas já vêm convertidas para UTC.</summary>
    public class FiltroDeSessoes
    {
        public Guid? PacienteId { get; init; }
        public Guid? TratamentoId { get; init; }
        public Guid? VeterinarioId { get; init; }
        public DateTime? InicioUtc { get; init; }
        public DateTime? FimUtc { get; init; }
    }

    public interface ISessaoRepository
    {
        Task Adicionar(Sessao sessao);

        /// <summary>
        /// RN-002: verifica se o intervalo [início, fim) sobrepõe outra sessão do veterinário. Sessões
        /// canceladas e inativadas liberam o horário.
        /// </summary>
        Task<bool> ExisteConflitoHorario(Guid veterinarioId, DateTime inicio, DateTime fim, Guid? ignorarSessaoId = null);

        Task<Sessao?> ObterPorId(Guid id);
        Task<Sessao?> ObterPorIdComRelacionamentos(Guid id);
        void Atualizar(Sessao sessao);
        Task SalvarAlteracoes();

        /// <summary>Agenda do veterinário em um intervalo, cobrindo as visões Dia, Semana e Mês (HU-004).</summary>
        Task<List<Sessao>> ObterPorPeriodo(Guid veterinarioId, DateTime inicio, DateTime fim);

        /// <summary>Agenda geral consolidada da clínica (HU-005).</summary>
        Task<List<Sessao>> ObterAgendaGeral(Guid clinicaId, DateTime inicio, DateTime fim, List<Guid>? veterinariosFiltrados);

        /// <summary>Sessões dos pets de um tutor (HU-013); as inativadas não fazem parte da agenda dele.</summary>
        Task<List<Sessao>> ObterPorTutor(Guid tutorId, bool apenasFuturas);

        Task<List<Sessao>> ObterPorTratamento(Guid tratamentoId, bool incluirInativas);

        /// <summary>Todas as sessões dos tratamentos de um paciente, para o prontuário.</summary>
        Task<List<Sessao>> ObterPorPaciente(Guid pacienteId, bool incluirInativas);

        /// <summary>Totais de vários tratamentos numa só consulta, para listas e prontuário.</summary>
        Task<Dictionary<Guid, ContagemDeSessoes>> ContarPorTratamentos(IEnumerable<Guid> tratamentosIds);

        /// <summary>Sessões ainda não confirmadas dentro da janela de lembrete (HU-015, CA-2).</summary>
        Task<List<Sessao>> ObterPendentesDeLembrete(DateTime limite);

        /// <summary>
        /// Tela de gestão: sessões da clínica filtradas e, quando informada, numa situação
        /// (<see cref="SituacaoSessao"/>). Sem situação, todas as que não foram inativadas.
        /// </summary>
        Task<PaginaDe<Sessao>> Listar(Guid clinicaId, FiltroDeSessoes filtro, string? situacao, DateTime agoraUtc, ParametrosPagina parametros);

        /// <summary>Quantas sessões há em cada situação, com os mesmos filtros da lista.</summary>
        Task<Dictionary<string, int>> ContarPorSituacao(Guid clinicaId, FiltroDeSessoes filtro, DateTime agoraUtc);
    }
}
