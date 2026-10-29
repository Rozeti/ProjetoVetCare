using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Atualização parcial: campos omitidos (nulos) mantêm o valor atual. Nome e e-mail só a
    /// equipe clínica altera; o próprio tutor mantém telefone e endereço em dia.
    /// </summary>
    public class AtualizarTutorDTO
    {
        [StringLength(120, ErrorMessage = "O nome deve ter até 120 caracteres.")]
        public string? Nome { get; set; }

        [StringLength(180, ErrorMessage = "O e-mail deve ter até 180 caracteres.")]
        public string? Email { get; set; }

        [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
        public string? Telefone { get; set; }

        [StringLength(250, ErrorMessage = "O endereço deve ter no máximo 250 caracteres.")]
        public string? Endereco { get; set; }

        [StringLength(20, ErrorMessage = "O CPF deve ter no máximo 20 caracteres.")]
        public string? Cpf { get; set; }
    }
}
