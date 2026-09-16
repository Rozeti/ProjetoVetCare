namespace VetCare.API.DTOs
{
    public class SessaoDTO
    {
        public Guid Id { get; set; }
        public Guid TratamentoId { get; set; }
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;
        public DateTime DataHora { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;

        /// <summary>Indica se já existe atendimento registrado para esta sessão (HU-008).</summary>
        public bool PossuiAtendimento { get; set; }

        /// <summary>RN-009: informa à interface se o tutor ainda está dentro do prazo de cancelamento.</summary>
        public bool PodeCancelar { get; set; }
    }
}
