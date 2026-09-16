namespace VetCare.API.Models
{
    public class Veterinario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public string Crmv { get; set; } = string.Empty;
        public string Especialidade { get; set; } = string.Empty;

        public Usuario? Usuario { get; set; }
    }
}