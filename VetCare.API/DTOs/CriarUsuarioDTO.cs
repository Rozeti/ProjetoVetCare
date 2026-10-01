using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// HU-002: cadastro feito pelo Administrador. Quando o perfil é Veterinario, Tutor ou
    /// Apoio, os dados profissionais correspondentes são criados na mesma operação.
    /// </summary>
    public class CriarUsuarioDTO
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Opcional. Em branco, o usuário recebe por e-mail um link e um código para criar a
        /// própria senha; preenchida, a clínica a comunica pessoalmente.
        /// </summary>
        [StringLength(64, ErrorMessage = "A senha deve ter no máximo 64 caracteres.")]
        public string? Senha { get; set; }

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
