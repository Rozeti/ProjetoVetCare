namespace VetCare.API.Models
{
    /// <summary>
    /// Primeiro contato veterinário de uma consulta. Um animal pode ter várias avaliações ao
    /// longo do tempo, e cada uma pode ou não originar tratamentos, receitas e pedidos de exame.
    /// </summary>
    public class AvaliacaoClinica
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid ProntuarioId { get; set; }
        public Guid VeterinarioId { get; set; }

        // Campos obrigatórios segundo a RN-010
        public string MotivoConsulta { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string DiagnosticoPossivel { get; set; } = string.Empty;

        public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
        public DateTime? DataUltimaEdicao { get; set; }

        public Pet? Paciente { get; set; }
        public Prontuario? Prontuario { get; set; }
        public Veterinario? Veterinario { get; set; }

        /// <summary>Tratamentos encaminhados a partir desta avaliação.</summary>
        public ICollection<Tratamento> Tratamentos { get; set; } = new List<Tratamento>();
    }
}
