namespace VetCare.API.Models
{
    /// <summary>
    /// Registro de vacina ou vermífugo aplicado ao paciente, com a data da próxima
    /// dose usada para gerar lembretes de prevenção.
    /// </summary>
    public class Vacina
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid? VeterinarioId { get; set; }

        /// <summary>Vacina, Vermifugo, Antipulgas ou Outro.</summary>
        public string Tipo { get; set; } = "Vacina";

        public string Nome { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public DateTime DataAplicacao { get; set; }
        public DateTime? ProximaDose { get; set; }
        public string Observacoes { get; set; } = string.Empty;
        public bool LembreteEnviado { get; set; }
        public DateTime DataRegistro { get; set; } = DateTime.UtcNow;

        public Pet? Paciente { get; set; }
        public Veterinario? Veterinario { get; set; }
    }
}
