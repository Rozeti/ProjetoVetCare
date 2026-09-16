namespace VetCare.API.DTOs
{
    public class AtendimentoDTO
    {
        public Guid Id { get; set; }
        public Guid SessaoId { get; set; }
        public Guid TratamentoId { get; set; }
        public Guid ProntuarioId { get; set; }
        public DateTime DataRegistro { get; set; }
        public DateTime? DataUltimaEdicao { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string TecnicasAplicadas { get; set; } = string.Empty;
        public int EscalaDor { get; set; }
        public string EvolucaoClinica { get; set; } = string.Empty;
        public string SinaisVitais { get; set; } = string.Empty;
        public string ProximosPassos { get; set; } = string.Empty;
        public decimal? PesoKg { get; set; }
        public decimal? TemperaturaCelsius { get; set; }
        public int? FrequenciaCardiaca { get; set; }
        public int? FrequenciaRespiratoria { get; set; }
        public List<MidiaDTO> Midias { get; set; } = new();
    }
}
