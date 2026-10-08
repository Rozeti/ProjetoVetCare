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
        [StringLength(180, ErrorMessage = "O e-mail deve ter até 180 caracteres.")]
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
        [StringLength(30, ErrorMessage = "O CRMV deve ter até 30 caracteres.")]
        public string? Crmv { get; set; }

        [StringLength(120, ErrorMessage = "A especialidade deve ter até 120 caracteres.")]
        public string? Especialidade { get; set; }

        // Dados do tutor (perfil Tutor)
        [StringLength(30, ErrorMessage = "O telefone deve ter até 30 caracteres.")]
        public string? Telefone { get; set; }

        [StringLength(250, ErrorMessage = "O endereço deve ter até 250 caracteres.")]
        public string? Endereco { get; set; }

        [StringLength(20, ErrorMessage = "O CPF deve ter até 20 caracteres.")]
        public string? Cpf { get; set; }

        // Dados do apoio administrativo (perfil Apoio)
        [StringLength(80, ErrorMessage = "O setor deve ter até 80 caracteres.")]
        public string? Setor { get; set; }
    }
}
