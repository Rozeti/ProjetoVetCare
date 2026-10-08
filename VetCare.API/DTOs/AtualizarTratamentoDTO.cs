using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>Atualização parcial: campos omitidos (nulos) mantêm o valor atual.</summary>
    public class AtualizarTratamentoDTO
    {
        [StringLength(1000, ErrorMessage = "O objetivo terapêutico deve ter no máximo 1000 caracteres.")]
        public string? ObjetivoTerapeutico { get; set; }

        [StringLength(2000, ErrorMessage = "As observações devem ter no máximo 2000 caracteres.")]
        public string? ObservacoesGerais { get; set; }

        /// <summary>Em Andamento, Concluído ou Interrompido.</summary>
        public string? Status { get; set; }

        public DateTime? DataFim { get; set; }
    }
}
