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
        public string Email { get; set; } = string.Empty;

        public string? Crmv { get; set; }
        public string? Especialidade { get; set; }
        public string? Telefone { get; set; }
        public string? Endereco { get; set; }
        public string? Cpf { get; set; }
        public string? Setor { get; set; }
    }
}
