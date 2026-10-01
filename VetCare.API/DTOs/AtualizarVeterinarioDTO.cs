using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarVeterinarioDTO
    {
        [Required(ErrorMessage = "O CRMV é obrigatório.")]
        public string Crmv { get; set; } = string.Empty;

        /// <summary>Omitida (nula) mantém a atual; vazia volta ao padrão da clínica.</summary>
        public string? Especialidade { get; set; }
    }
}
