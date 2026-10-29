namespace VetCare.API.DTOs
{
    public class TratamentoDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;

        /// <summary>Avaliação de origem do tratamento.</summary>
        public Guid AvaliacaoId { get; set; }
        public DateTime? DataAvaliacao { get; set; }
        public string MotivoAvaliacao { get; set; } = string.Empty;

        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string DuracaoPrevista { get; set; } = string.Empty;
        public string Frequencia { get; set; } = string.Empty;
        public int SessoesPrevistas { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ObservacoesGerais { get; set; } = string.Empty;

        /// <summary>Sessões agendadas na agenda para este tratamento.</summary>
        public int TotalSessoes { get; set; }
        public int SessoesConcluidas { get; set; }

        /// <summary>Evoluções (sessões realizadas) registradas no tratamento.</summary>
        public int EvolucoesRegistradas { get; set; }
    }
}
