namespace VetCare.API.DTOs
{
    public class AvaliacaoDTO
    {
        public Guid Id { get; set; }
        public Guid TratamentoId { get; set; }
        public Guid ProntuarioId { get; set; }
        public DateTime DataRegistro { get; set; }
        public DateTime? DataUltimaEdicao { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string QueixaPrincipal { get; set; } = string.Empty;
        public string Anamnese { get; set; } = string.Empty;
        public string ExameFisico { get; set; } = string.Empty;
        public string HipoteseDiagnostica { get; set; } = string.Empty;
        public string PlanoTerapeutico { get; set; } = string.Empty;
    }
}
