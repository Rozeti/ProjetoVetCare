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

        public string? Nome { get; set; }

        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string? Email { get; set; }

        public string? Senha { get; set; }

        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
    }
}
