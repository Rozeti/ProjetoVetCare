using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarStatusSessaoDTO
    {
        /// <summary>Confirmada, Cancelada ou Concluída.</summary>
        [Required(ErrorMessage = "O status é obrigatório.")]
        public string Status { get; set; } = string.Empty;

        public string? Motivo { get; set; }
    }
}
