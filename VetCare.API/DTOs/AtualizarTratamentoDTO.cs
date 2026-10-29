using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>Atualização parcial: campos omitidos (nulos) mantêm o valor atual.</summary>
    public class AtualizarTratamentoDTO
    {
        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome do tratamento deve ter entre 3 e 120 caracteres.")]
        public string? Nome { get; set; }

        [StringLength(60, ErrorMessage = "O tipo do tratamento deve ter até 60 caracteres.")]
        public string? Tipo { get; set; }

        [StringLength(2000, ErrorMessage = "A descrição deve ter no máximo 2000 caracteres.")]
        public string? Descricao { get; set; }

        [StringLength(60, ErrorMessage = "A duração prevista deve ter até 60 caracteres.")]
        public string? DuracaoPrevista { get; set; }

        [StringLength(60, ErrorMessage = "A frequência deve ter até 60 caracteres.")]
        public string? Frequencia { get; set; }

        [Range(1, 365, ErrorMessage = "Informe entre 1 e 365 sessões previstas.")]
        public int? SessoesPrevistas { get; set; }

        [StringLength(2000, ErrorMessage = "As observações devem ter no máximo 2000 caracteres.")]
        public string? ObservacoesGerais { get; set; }

        public DateTime? DataInicio { get; set; }

        /// <summary>Planejado, Em Andamento, Concluído ou Inativado.</summary>
        public string? Status { get; set; }

        public DateTime? DataFim { get; set; }
    }
}
