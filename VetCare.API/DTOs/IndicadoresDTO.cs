namespace VetCare.API.DTOs
{
    /// <summary>
    /// HU-016: indicadores operacionais do dia. O escopo dos números respeita a RN-008 —
    /// o veterinário vê apenas os próprios pacientes e o administrador vê a clínica toda.
    /// </summary>
    public class IndicadoresDTO
    {
        public DateTime Data { get; set; }

        /// <summary>Clinica ou Veterinario.</summary>
        public string Escopo { get; set; } = string.Empty;

        public int AvaliacoesDoDia { get; set; }
        public int AtendimentosDoDia { get; set; }
        public int SessoesDoDia { get; set; }
        public int ConfirmacoesPendentes { get; set; }
        public int PacientesAtivos { get; set; }
        public int MensagensNaoLidas { get; set; }
        public int NotificacoesNaoVisualizadas { get; set; }

        /// <summary>Próximas sessões do dia, para o atalho operacional do painel.</summary>
        public List<ItemAgendaDTO> ProximasSessoes { get; set; } = new();
    }
}
