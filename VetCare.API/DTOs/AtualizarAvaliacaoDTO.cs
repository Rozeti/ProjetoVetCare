using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-007, CA-4: correção mantendo a rastreabilidade da versão anterior (RN-004).</summary>
    public class AtualizarAvaliacaoDTO
    {
        [Required(ErrorMessage = "O motivo da consulta é obrigatório.")]
        [StringLength(2000, ErrorMessage = "O motivo da consulta deve ter até 2000 caracteres.")]
        public string MotivoConsulta { get; set; } = string.Empty;

        [Required(ErrorMessage = "As observações são obrigatórias.")]
        [StringLength(8000, ErrorMessage = "As observações devem ter até 8000 caracteres.")]
        public string Observacoes { get; set; } = string.Empty;

        [Required(ErrorMessage = "O possível diagnóstico é obrigatório.")]
        [StringLength(2000, ErrorMessage = "O possível diagnóstico deve ter até 2000 caracteres.")]
        public string DiagnosticoPossivel { get; set; } = string.Empty;
    }
}
