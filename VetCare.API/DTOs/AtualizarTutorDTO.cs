using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>Atualização parcial dos dados de contato: campos omitidos (nulos) mantêm o valor atual.</summary>
    public class AtualizarTutorDTO
    {
        [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
        public string? Telefone { get; set; }

        [StringLength(250, ErrorMessage = "O endereço deve ter no máximo 250 caracteres.")]
        public string? Endereco { get; set; }

        [StringLength(20, ErrorMessage = "O CPF deve ter no máximo 20 caracteres.")]
        public string? Cpf { get; set; }
    }
}
