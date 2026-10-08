using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarVeterinarioDTO
    {
        [Required(ErrorMessage = "O CRMV é obrigatório.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "O CRMV deve ter entre 3 e 30 caracteres.")]
        public string Crmv { get; set; } = string.Empty;

        /// <summary>Omitida (nula) mantém a atual; vazia volta ao padrão da clínica.</summary>
        [StringLength(120, ErrorMessage = "A especialidade deve ter até 120 caracteres.")]
        public string? Especialidade { get; set; }
    }
}
