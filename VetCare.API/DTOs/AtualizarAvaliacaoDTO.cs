using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-007, CA-4: correção mantendo a rastreabilidade da versão anterior (RN-004).</summary>
    public class AtualizarAvaliacaoDTO
    {
        [Required(ErrorMessage = "A queixa principal é obrigatória.")]
        [StringLength(2000, ErrorMessage = "A queixa principal deve ter até 2000 caracteres.")]
        public string QueixaPrincipal { get; set; } = string.Empty;

        [Required(ErrorMessage = "A anamnese é obrigatória.")]
        [StringLength(4000, ErrorMessage = "A anamnese deve ter até 4000 caracteres.")]
        public string Anamnese { get; set; } = string.Empty;

        [Required(ErrorMessage = "O exame físico é obrigatório.")]
        [StringLength(4000, ErrorMessage = "O exame físico deve ter até 4000 caracteres.")]
        public string ExameFisico { get; set; } = string.Empty;

        [Required(ErrorMessage = "A hipótese diagnóstica é obrigatória.")]
        [StringLength(2000, ErrorMessage = "A hipótese diagnóstica deve ter até 2000 caracteres.")]
        public string HipoteseDiagnostica { get; set; } = string.Empty;

        [Required(ErrorMessage = "O plano terapêutico é obrigatório.")]
        [StringLength(4000, ErrorMessage = "O plano terapêutico deve ter até 4000 caracteres.")]
        public string PlanoTerapeutico { get; set; } = string.Empty;
    }
}
