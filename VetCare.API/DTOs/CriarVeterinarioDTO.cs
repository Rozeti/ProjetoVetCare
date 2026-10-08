using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Cria o veterinário junto com o usuário de acesso. Se UsuarioId for informado,
    /// vincula o registro profissional a um usuário já existente.
    /// </summary>
    public class CriarVeterinarioDTO
    {
        public Guid? UsuarioId { get; set; }

        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 120 caracteres.")]
        public string? Nome { get; set; }

        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(180, ErrorMessage = "O e-mail deve ter até 180 caracteres.")]
        public string? Email { get; set; }

        [StringLength(64, ErrorMessage = "A senha deve ter no máximo 64 caracteres.")]
        public string? Senha { get; set; }

        [Required(ErrorMessage = "O CRMV é obrigatório.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "O CRMV deve ter entre 3 e 30 caracteres.")]
        public string Crmv { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "A especialidade deve ter até 120 caracteres.")]
        public string Especialidade { get; set; } = string.Empty;
    }
}
