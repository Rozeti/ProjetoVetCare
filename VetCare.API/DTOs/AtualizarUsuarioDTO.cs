using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-002, CA-3: edição de dados preservando o histórico de vínculos.</summary>
    public class AtualizarUsuarioDTO
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(180, ErrorMessage = "O e-mail deve ter até 180 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "O CRMV deve ter até 30 caracteres.")]
        public string? Crmv { get; set; }

        [StringLength(120, ErrorMessage = "A especialidade deve ter até 120 caracteres.")]
        public string? Especialidade { get; set; }

        [StringLength(80, ErrorMessage = "O setor deve ter até 80 caracteres.")]
        public string? Setor { get; set; }
    }
}
