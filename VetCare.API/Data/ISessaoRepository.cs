using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ISessaoRepository
    {
        Task Adicionar(Sessao sessao);

        /// <summary>
        /// RN-002: verifica sobreposição considerando a duração da sessão, e não apenas
        /// a igualdade exata de horário.
        /// </summary>
        Task<bool> ExisteConflitoHorario(Guid veterinarioId, DateTime dataHora, int duracaoMinutos, Guid? ignorarSessaoId = null);

        Task<Sessao?> ObterPorId(Guid id);
        Task<Sessao?> ObterPorIdComRelacionamentos(Guid id);
        void Atualizar(Sessao sessao);
        Task SalvarAlteracoes();

        Task<List<Sessao>> ObterSessoesDoDia(Guid veterinarioId, DateTime data);

        /// <summary>Agenda do veterinário em um intervalo, cobrindo as visões Dia, Semana e Mês (HU-004).</summary>
        Task<List<Sessao>> ObterPorPeriodo(Guid veterinarioId, DateTime inicio, DateTime fim);

        /// <summary>Agenda geral consolidada da clínica (HU-005).</summary>
        Task<List<Sessao>> ObterAgendaGeral(Guid clinicaId, DateTime inicio, DateTime fim, List<Guid>? veterinariosFiltrados);

        /// <summary>Sessões dos pets de um tutor (HU-013).</summary>
        Task<List<Sessao>> ObterPorTutor(Guid tutorId, bool apenasFuturas);

        Task<List<Sessao>> ObterPorTratamento(Guid tratamentoId);

        /// <summary>Sessões ainda não confirmadas dentro da janela de lembrete (HU-015, CA-2).</summary>
        Task<List<Sessao>> ObterPendentesDeLembrete(DateTime limite);
    }
}
