using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>RN-004: a receita é cancelada, nunca apagada; o motivo fica no histórico.</summary>
    public class CancelarPrescricaoDTO
    {
        [StringLength(300, ErrorMessage = "O motivo deve ter no máximo 300 caracteres.")]
        public string? Motivo { get; set; }
    }
}
