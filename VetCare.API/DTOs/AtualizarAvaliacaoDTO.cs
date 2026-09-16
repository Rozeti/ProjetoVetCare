using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-007, CA-4: correção mantendo a rastreabilidade da versão anterior (RN-004).</summary>
    public class AtualizarAvaliacaoDTO
    {
        [Required(ErrorMessage = "A queixa principal é obrigatória.")]
        public string QueixaPrincipal { get; set; } = string.Empty;

        [Required(ErrorMessage = "A anamnese é obrigatória.")]
        public string Anamnese { get; set; } = string.Empty;

        [Required(ErrorMessage = "O exame físico é obrigatório.")]
        public string ExameFisico { get; set; } = string.Empty;

        [Required(ErrorMessage = "A hipótese diagnóstica é obrigatória.")]
        public string HipoteseDiagnostica { get; set; } = string.Empty;

        [Required(ErrorMessage = "O plano terapêutico é obrigatório.")]
        public string PlanoTerapeutico { get; set; } = string.Empty;
    }
}
