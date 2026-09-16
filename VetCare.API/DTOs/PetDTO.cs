namespace VetCare.API.DTOs
{
    public class PetDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public string Raca { get; set; } = string.Empty;
        public string Sexo { get; set; } = string.Empty;
        public string Pelagem { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public bool Castrado { get; set; }
        public DateTime DataNascimento { get; set; }
        public int IdadeAnos { get; set; }

        /// <summary>Idade formatada para leitura, incluindo meses em animais filhotes.</summary>
        public string IdadeDescritiva { get; set; } = string.Empty;

        public decimal? PesoAtualKg { get; set; }
        public Guid TutorId { get; set; }
        public string NomeTutor { get; set; } = string.Empty;
        public string TelefoneTutor { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public DateTime? DataObito { get; set; }

        /// <summary>Alergias e comorbidades ativas, exibidas em destaque no atendimento.</summary>
        public List<AlergiaDTO> AlertasClinicos { get; set; } = new();

        public int VacinasVencidas { get; set; }
    }
}
