namespace VetCare.API.Models
{
    /// <summary>
    /// Período em que o veterinário não atende: férias, congresso, almoço ou
    /// indisponibilidade pontual. Bloqueia o agendamento junto com a RN-002.
    /// </summary>
    public class BloqueioAgenda
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid VeterinarioId { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public Guid CriadoPorId { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public Veterinario? Veterinario { get; set; }
    }
}
