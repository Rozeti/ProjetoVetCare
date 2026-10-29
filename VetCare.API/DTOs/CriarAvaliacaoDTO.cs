using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Primeiro contato clínico de uma consulta. A avaliação pertence ao animal e não depende de
    /// tratamento: é ela que, depois, pode originar tratamentos, receitas e pedidos de exame.
    /// </summary>
    public class CriarAvaliacaoDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        /// <summary>Opcional: o veterinário autenticado, ou o responsável pelo paciente, quando não informado.</summary>
        public Guid? VeterinarioId { get; set; }

        // RN-010: os três campos abaixo são obrigatórios para concluir a avaliação.
        [Required(ErrorMessage = "O motivo da consulta é obrigatório.")]
        [StringLength(2000, ErrorMessage = "O motivo da consulta deve ter até 2000 caracteres.")]
        public string MotivoConsulta { get; set; } = string.Empty;

        [Required(ErrorMessage = "As observações são obrigatórias.")]
        [StringLength(4000, ErrorMessage = "As observações devem ter até 4000 caracteres.")]
        public string Observacoes { get; set; } = string.Empty;

        [Required(ErrorMessage = "O possível diagnóstico é obrigatório.")]
        [StringLength(2000, ErrorMessage = "O possível diagnóstico deve ter até 2000 caracteres.")]
        public string DiagnosticoPossivel { get; set; } = string.Empty;

        /// <summary>HU-009: observação restrita registrada junto com a avaliação.</summary>
        [StringLength(2000, ErrorMessage = "A observação interna deve ter até 2000 caracteres.")]
        public string? ObservacaoInterna { get; set; }
    }
}
