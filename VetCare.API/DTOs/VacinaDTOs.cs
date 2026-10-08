using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarVacinaDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe o tipo: Vacina, Vermifugo, Antipulgas ou Outro.")]
        public string Tipo { get; set; } = "Vacina";

        [Required(ErrorMessage = "O nome do produto é obrigatório.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome do produto deve ter entre 2 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "O fabricante deve ter no máximo 120 caracteres.")]
        public string Fabricante { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "O lote deve ter no máximo 60 caracteres.")]
        public string Lote { get; set; } = string.Empty;

        /// <summary>Posição desta dose no esquema. Informe junto com o total de doses.</summary>
        [Range(1, 10, ErrorMessage = "O número da dose deve estar entre 1 e 10.")]
        public int? NumeroDose { get; set; }

        [Range(1, 10, ErrorMessage = "O esquema deve ter entre 1 e 10 doses.")]
        public int? TotalDoses { get; set; }

        /// <summary>Nenhuma, Mensal, Trimestral, Semestral ou Anual.</summary>
        public string Recorrencia { get; set; } = "Nenhuma";

        [Required(ErrorMessage = "A data de aplicação é obrigatória.")]
        public DateTime DataAplicacao { get; set; }

        public DateTime? ProximaDose { get; set; }

        [StringLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string Observacoes { get; set; } = string.Empty;
    }

    public class AtualizarVacinaDTO
    {
        /// <summary>Quem aplicou. Vazio mantém o registro atual.</summary>
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe o tipo do produto.")]
        public string Tipo { get; set; } = "Vacina";

        [Required(ErrorMessage = "O nome do produto é obrigatório.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome do produto deve ter entre 2 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "O fabricante deve ter no máximo 120 caracteres.")]
        public string Fabricante { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "O lote deve ter no máximo 60 caracteres.")]
        public string Lote { get; set; } = string.Empty;

        [Range(1, 10, ErrorMessage = "O número da dose deve estar entre 1 e 10.")]
        public int? NumeroDose { get; set; }

        [Range(1, 10, ErrorMessage = "O esquema deve ter entre 1 e 10 doses.")]
        public int? TotalDoses { get; set; }

        public string Recorrencia { get; set; } = "Nenhuma";

        [Required(ErrorMessage = "A data de aplicação é obrigatória.")]
        public DateTime DataAplicacao { get; set; }

        public DateTime? ProximaDose { get; set; }

        [StringLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string Observacoes { get; set; } = string.Empty;
    }

    /// <summary>A carteira é um documento do animal: apagar um registro exige dizer por quê.</summary>
    public class ExcluirVacinaDTO
    {
        [Required(ErrorMessage = "Informe a justificativa da exclusão.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "A justificativa deve ter entre 5 e 300 caracteres.")]
        public string Justificativa { get; set; } = string.Empty;
    }

    public class VacinaDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public int? NumeroDose { get; set; }
        public int? TotalDoses { get; set; }

        /// <summary>"Dose 2 de 3" para esquemas; vazio numa aplicação avulsa.</summary>
        public string DescricaoDose { get; set; } = string.Empty;

        public string Recorrencia { get; set; } = string.Empty;
        public DateTime DataAplicacao { get; set; }
        public DateTime? ProximaDose { get; set; }
        public string Observacoes { get; set; } = string.Empty;
        public Guid? VeterinarioId { get; set; }
        public string AplicadaPor { get; set; } = string.Empty;

        /// <summary>Em dia, A vencer, Vencida, Dose única ou Concluída (a dose seguinte já foi aplicada).</summary>
        public string SituacaoDose { get; set; } = string.Empty;

        public int? DiasParaProximaDose { get; set; }
    }
}
