using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarStatusSessaoDTO
    {
        /// <summary>Confirmada, Cancelada ou Concluída.</summary>
        [Required(ErrorMessage = "O status é obrigatório.")]
        public string Status { get; set; } = string.Empty;

        /// <summary>Anotado nas observações da sessão e na auditoria.</summary>
        [StringLength(300, ErrorMessage = "O motivo deve ter até 300 caracteres.")]
        public string? Motivo { get; set; }
    }
}
