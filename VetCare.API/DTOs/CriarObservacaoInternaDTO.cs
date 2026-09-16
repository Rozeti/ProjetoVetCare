using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-009: anotação clínica privada, nunca exibida ao Tutor (RN-003).</summary>
    public class CriarObservacaoInternaDTO
    {
        public Guid? ProntuarioId { get; set; }
        public Guid? PacienteId { get; set; }
        public Guid? AvaliacaoId { get; set; }
        public Guid? AtendimentoId { get; set; }

        [Required(ErrorMessage = "O conteúdo da observação é obrigatório.")]
        [StringLength(2000, MinimumLength = 1)]
        public string Conteudo { get; set; } = string.Empty;
    }
}
