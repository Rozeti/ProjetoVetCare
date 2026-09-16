using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarAvaliacaoDTO
    {
        [Required(ErrorMessage = "O tratamento é obrigatório.")]
        public Guid TratamentoId { get; set; }

        /// <summary>Opcional: o prontuário do paciente é localizado ou aberto automaticamente.</summary>
        public Guid? ProntuarioId { get; set; }

        public Guid? VeterinarioId { get; set; }

        // RN-010: os cinco campos abaixo são obrigatórios para concluir a avaliação.
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

        /// <summary>HU-009: observação restrita registrada junto com a avaliação.</summary>
        public string? ObservacaoInterna { get; set; }
    }
}
