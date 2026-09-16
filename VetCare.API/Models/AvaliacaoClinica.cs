namespace VetCare.API.Models
{
    public class AvaliacaoClinica
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TratamentoId { get; set; }
        public Guid ProntuarioId { get; set; }
        public Guid VeterinarioId { get; set; }

        // Campos obrigatórios segundo a RN-010
        public string QueixaPrincipal { get; set; } = string.Empty;
        public string Anamnese { get; set; } = string.Empty;
        public string ExameFisico { get; set; } = string.Empty;
        public string HipoteseDiagnostica { get; set; } = string.Empty;
        public string PlanoTerapeutico { get; set; } = string.Empty;

        public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
        public DateTime? DataUltimaEdicao { get; set; }

        public Tratamento? Tratamento { get; set; }
        public Prontuario? Prontuario { get; set; }
        public Veterinario? Veterinario { get; set; }
    }
}
