using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarVeterinarioDTO
    {
        public Guid? UsuarioId { get; set; }

        public string? Nome { get; set; }

        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string? Email { get; set; }

        public string? Senha { get; set; }

        [Required(ErrorMessage = "O CRMV é obrigatório.")]
        public string Crmv { get; set; } = string.Empty;

        public string Especialidade { get; set; } = string.Empty;
    }
}
