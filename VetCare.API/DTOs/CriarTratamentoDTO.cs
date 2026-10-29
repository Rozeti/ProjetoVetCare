using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// O tratamento é encaminhado a partir de uma avaliação: o paciente vem dela, e sem ela o
    /// tratamento não existe.
    /// </summary>
    public class CriarTratamentoDTO
    {
        [Required(ErrorMessage = "A avaliação de origem é obrigatória.")]
        public Guid AvaliacaoId { get; set; }

        /// <summary>Opcional: o responsável pelo paciente, ou o veterinário autenticado.</summary>
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "O nome do tratamento é obrigatório.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome do tratamento deve ter entre 3 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O tipo do tratamento é obrigatório.")]
        [StringLength(60, ErrorMessage = "O tipo do tratamento deve ter até 60 caracteres.")]
        public string Tipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "A descrição do tratamento é obrigatória.")]
        [StringLength(2000, MinimumLength = 3, ErrorMessage = "A descrição deve ter entre 3 e 2000 caracteres.")]
        public string Descricao { get; set; } = string.Empty;

        [Required(ErrorMessage = "A duração prevista é obrigatória.")]
        [StringLength(60, ErrorMessage = "A duração prevista deve ter até 60 caracteres.")]
        public string DuracaoPrevista { get; set; } = string.Empty;

        [Required(ErrorMessage = "A frequência é obrigatória.")]
        [StringLength(60, ErrorMessage = "A frequência deve ter até 60 caracteres.")]
        public string Frequencia { get; set; } = string.Empty;

        [Range(1, 365, ErrorMessage = "Informe entre 1 e 365 sessões previstas.")]
        public int SessoesPrevistas { get; set; }

        /// <summary>Sem valor, o tratamento começa hoje. Só um tratamento planejado pode começar no futuro.</summary>
        public DateTime? DataInicio { get; set; }

        /// <summary>Planejado (padrão) ou Em Andamento.</summary>
        public string? Status { get; set; }

        [StringLength(2000, ErrorMessage = "As observações devem ter no máximo 2000 caracteres.")]
        public string ObservacoesGerais { get; set; } = string.Empty;
    }
}
