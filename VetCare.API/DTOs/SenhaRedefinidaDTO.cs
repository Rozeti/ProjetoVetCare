namespace VetCare.API.DTOs
{
    /// <summary>HU-002, CA-5: retorno da redefinição de senha feita pelo Administrador.</summary>
    public class SenhaRedefinidaDTO
    {
        public Guid UsuarioId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string SenhaProvisoria { get; set; } = string.Empty;
    }
}
