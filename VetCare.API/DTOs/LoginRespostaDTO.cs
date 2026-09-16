namespace VetCare.API.DTOs
{
    public class LoginRespostaDTO
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraEm { get; set; }
        public UsuarioDTO Usuario { get; set; } = new();
    }
}
