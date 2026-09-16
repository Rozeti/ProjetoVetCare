namespace VetCare.API.Models
{
    public class Pet
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ClinicaId { get; set; }
        public Guid TutorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public string Raca { get; set; } = string.Empty;
        public string Sexo { get; set; } = string.Empty;
        public string Pelagem { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
        public decimal? PesoAtualKg { get; set; }

        /// <summary>Identificação eletrônica, quando o animal possui.</summary>
        public string Microchip { get; set; } = string.Empty;

        public bool Castrado { get; set; }

        /// <summary>HU-003, CA-4: pacientes com prontuário são inativados, nunca excluídos.</summary>
        public bool Ativo { get; set; } = true;

        public DateTime? DataObito { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

        public Tutor? Tutor { get; set; }
        public Clinica? Clinica { get; set; }
        public ICollection<AlergiaCondicao> AlergiasCondicoes { get; set; } = new List<AlergiaCondicao>();
        public ICollection<Vacina> Vacinas { get; set; } = new List<Vacina>();
    }
}
