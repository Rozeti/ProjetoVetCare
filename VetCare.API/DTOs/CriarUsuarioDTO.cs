using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// HU-002: cadastro feito pelo Administrador. Quando o perfil é Veterinario ou Tutor,
    /// os dados profissionais correspondentes são criados na mesma operação.
    /// </summary>
    public class CriarUsuarioDTO
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [StringLength(64, MinimumLength = 6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "O perfil é obrigatório.")]
        public string Perfil { get; set; } = string.Empty;

        // Dados do veterinário (perfil Veterinario)
        public string? Crmv { get; set; }
        public string? Especialidade { get; set; }

        // Dados do tutor (perfil Tutor)
        public string? Telefone { get; set; }
        public string? Endereco { get; set; }
        public string? Cpf { get; set; }

        // Dados do apoio administrativo (perfil Apoio)
        public string? Setor { get; set; }
    }
}
