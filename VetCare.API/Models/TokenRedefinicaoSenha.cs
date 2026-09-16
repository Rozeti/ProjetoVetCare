namespace VetCare.API.Models
{
    /// <summary>
    /// Token de uso único para a redefinição de senha solicitada pelo próprio usuário.
    /// Guardamos apenas o hash do token; o valor original só existe no link enviado.
    /// </summary>
    public class TokenRedefinicaoSenha
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiraEm { get; set; }
        public DateTime? UtilizadoEm { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public Usuario? Usuario { get; set; }

        public bool Valido => UtilizadoEm == null && ExpiraEm > DateTime.UtcNow;
    }
}
