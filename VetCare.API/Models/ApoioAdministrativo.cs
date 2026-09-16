namespace VetCare.API.Models
{
    /// <summary>
    /// Perfil operacional da recepção: auxilia em cadastros, agenda e confirmações,
    /// sem acesso às configurações administrativas.
    /// </summary>
    public class ApoioAdministrativo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public string Setor { get; set; } = string.Empty;

        public Usuario? Usuario { get; set; }
    }
}
