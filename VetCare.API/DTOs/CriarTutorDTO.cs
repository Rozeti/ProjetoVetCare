using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Cria o tutor junto com o usuário de acesso. Se UsuarioId for informado, vincula o
    /// perfil a um usuário já existente em vez de criar um novo.
    /// </summary>
    public class CriarTutorDTO
    {
        public Guid? UsuarioId { get; set; }

        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 120 caracteres.")]
        public string? Nome { get; set; }

        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(180, ErrorMessage = "O e-mail deve ter até 180 caracteres.")]
        public string? Email { get; set; }

        [StringLength(64, ErrorMessage = "A senha deve ter no máximo 64 caracteres.")]
        public string? Senha { get; set; }

        [StringLength(30, ErrorMessage = "O telefone deve ter até 30 caracteres.")]
        public string Telefone { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "O endereço deve ter até 250 caracteres.")]
        public string Endereco { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "O CPF deve ter até 20 caracteres.")]
        public string Cpf { get; set; } = string.Empty;
    }
}
