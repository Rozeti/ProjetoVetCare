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
        [StringLength(120, MinimumLength = 2)]
        public string Nome { get; set; } = string.Empty;

        public string Fabricante { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de aplicação é obrigatória.")]
        public DateTime DataAplicacao { get; set; }

        public DateTime? ProximaDose { get; set; }
        public string Observacoes { get; set; } = string.Empty;
    }

    public class AtualizarVacinaDTO
    {
        [Required(ErrorMessage = "Informe o tipo do produto.")]
        public string Tipo { get; set; } = "Vacina";

        [Required(ErrorMessage = "O nome do produto é obrigatório.")]
        public string Nome { get; set; } = string.Empty;

        public string Fabricante { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public DateTime DataAplicacao { get; set; }
        public DateTime? ProximaDose { get; set; }
        public string Observacoes { get; set; } = string.Empty;
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
        public DateTime DataAplicacao { get; set; }
        public DateTime? ProximaDose { get; set; }
        public string Observacoes { get; set; } = string.Empty;
        public string AplicadaPor { get; set; } = string.Empty;

        /// <summary>Em dia, A vencer, Vencida ou Dose única.</summary>
        public string SituacaoDose { get; set; } = string.Empty;

        public int? DiasParaProximaDose { get; set; }
    }
}
