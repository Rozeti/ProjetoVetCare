namespace VetCare.API.Models
{
    /// <summary>
    /// Alergia, comorbidade ou restrição do paciente. Fica em destaque no prontuário
    /// porque precisa ser considerada antes de qualquer conduta clínica.
    /// </summary>
    public class AlergiaCondicao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid RegistradoPorId { get; set; }

        /// <summary>Alergia, Comorbidade, Restricao ou Cirurgia.</summary>
        public string Tipo { get; set; } = "Alergia";

        public string Descricao { get; set; } = string.Empty;

        /// <summary>Leve, Moderada ou Grave. Determina o destaque na interface.</summary>
        public string Gravidade { get; set; } = "Moderada";

        public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
        public bool Ativa { get; set; } = true;

        public Pet? Paciente { get; set; }
        public Usuario? RegistradoPor { get; set; }
    }
}
