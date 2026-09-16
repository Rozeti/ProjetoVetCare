namespace VetCare.API.DTOs
{
    public class ItemAgendaDTO
    {
        public Guid SessaoId { get; set; }
        public Guid PacienteId { get; set; }
        public Guid TratamentoId { get; set; }
        public Guid VeterinarioId { get; set; }
        public DateTime DataHora { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;
        public string NomeVeterinario { get; set; } = string.Empty;

        /// <summary>HU-005, CA-1: consolidação da agenda geral diferenciando cada veterinário por cor.</summary>
        public string CorVeterinario { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public bool PossuiAtendimento { get; set; }
    }
}
