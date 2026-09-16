using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarVeterinarioDTO
    {
        [Required(ErrorMessage = "O CRMV é obrigatório.")]
        public string Crmv { get; set; } = string.Empty;

        public string Especialidade { get; set; } = string.Empty;
    }
}
