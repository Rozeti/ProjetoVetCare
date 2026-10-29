namespace VetCare.API.DTOs
{
    /// <summary>Evolução: o registro completo de uma sessão realizada no tratamento.</summary>
    public class AtendimentoDTO
    {
        public Guid Id { get; set; }
        public Guid SessaoId { get; set; }
        public Guid TratamentoId { get; set; }
        public string NomeTratamento { get; set; } = string.Empty;
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public Guid ProntuarioId { get; set; }
        public int NumeroSessao { get; set; }
        public DateTime DataRegistro { get; set; }
        public DateTime? DataUltimaEdicao { get; set; }
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string TecnicasAplicadas { get; set; } = string.Empty;
        public int EscalaDor { get; set; }
        public string EvolucaoClinica { get; set; } = string.Empty;
        public string ReacaoAnimal { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string Relatorio { get; set; } = string.Empty;
        public string SinaisVitais { get; set; } = string.Empty;
        public string ProximosPassos { get; set; } = string.Empty;
        public decimal? PesoKg { get; set; }
        public decimal? TemperaturaCelsius { get; set; }
        public int? FrequenciaCardiaca { get; set; }
        public int? FrequenciaRespiratoria { get; set; }
        public List<MidiaDTO> Midias { get; set; } = new();
    }

    /// <summary>Linha da tela de evoluções: o suficiente para localizar e abrir o registro.</summary>
    public class EvolucaoResumoDTO
    {
        public Guid Id { get; set; }
        public Guid SessaoId { get; set; }
        public Guid TratamentoId { get; set; }
        public string NomeTratamento { get; set; } = string.Empty;
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public int NumeroSessao { get; set; }
        public DateTime DataRegistro { get; set; }
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string TecnicasAplicadas { get; set; } = string.Empty;
        public string EvolucaoClinica { get; set; } = string.Empty;
        public string ReacaoAnimal { get; set; } = string.Empty;
        public int EscalaDor { get; set; }
        public bool Editado { get; set; }
    }
}
