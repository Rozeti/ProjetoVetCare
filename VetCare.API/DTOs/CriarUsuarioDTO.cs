using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// HU-002: cadastro da equipe feito pelo Administrador (Administrador, Veterinario ou
    /// Apoio); os dados profissionais do perfil são criados na mesma operação. Tutores são
    /// cadastrados em /api/tutores. As regras de formato estão em ValidacaoDeCadastro.
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
        /// Opcional. Em branco, o usuário recebe por e-mail um código para criar a
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

        // Dados do apoio administrativo (perfil Apoio)
        [StringLength(80, ErrorMessage = "O setor deve ter até 80 caracteres.")]
        public string? Setor { get; set; }
    }
}
