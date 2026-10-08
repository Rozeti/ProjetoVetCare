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

        /// <summary>HU-009: observação restrita registrada junto com a avaliação.</summary>
        [StringLength(2000, ErrorMessage = "A observação interna deve ter até 2000 caracteres.")]
        public string? ObservacaoInterna { get; set; }
    }
}
