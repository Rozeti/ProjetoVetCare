using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>Enviado pelo aplicativo logo após o login, com o token de push do aparelho.</summary>
    public class RegistrarDispositivoDTO
    {
        [Required(ErrorMessage = "Informe o token de notificação do aparelho.")]
        [StringLength(200)]
        public string TokenPush { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a plataforma do aparelho.")]
        public string Plataforma { get; set; } = string.Empty;

        [StringLength(120)]
        public string? NomeDoAparelho { get; set; }
    }

    public class DispositivoDTO
    {
        public Guid Id { get; set; }
        public string Plataforma { get; set; } = string.Empty;
        public string NomeDoAparelho { get; set; } = string.Empty;
        public DateTime RegistradoEm { get; set; }
        public DateTime UltimoUsoEm { get; set; }
    }
}
