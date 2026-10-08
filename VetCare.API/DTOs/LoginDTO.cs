using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(180, ErrorMessage = "O e-mail deve ter até 180 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [StringLength(64, ErrorMessage = "A senha deve ter no máximo 64 caracteres.")]
        public string Senha { get; set; } = string.Empty;
    }
}
