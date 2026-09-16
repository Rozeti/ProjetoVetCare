namespace VetCare.API.DTOs
{
    public class TratamentoDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string ObjetivoTerapeutico { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ObservacoesGerais { get; set; } = string.Empty;
        public int TotalSessoes { get; set; }
        public int SessoesConcluidas { get; set; }
    }
}
