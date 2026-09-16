namespace VetCare.API.Models
{
    /// <summary>HU-014: troca de mensagens entre tutor e veterinário.</summary>
    public class Mensagem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RemetenteId { get; set; }
        public Guid DestinatarioId { get; set; }
        public Guid? PacienteId { get; set; }
        public string Conteudo { get; set; } = string.Empty;
        public DateTime DataEnvio { get; set; } = DateTime.UtcNow;
        public bool Lida { get; set; }

        public Usuario? Remetente { get; set; }
        public Usuario? Destinatario { get; set; }
        public Pet? Paciente { get; set; }
    }
}
