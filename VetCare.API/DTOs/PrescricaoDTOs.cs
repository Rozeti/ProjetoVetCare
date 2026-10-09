using System.ComponentModel.DataAnnotations;
using VetCare.API.Common;

namespace VetCare.API.DTOs
{
    public class CriarPrescricaoDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        public Guid? VeterinarioId { get; set; }
        public Guid? AtendimentoId { get; set; }
        public DateTime? ValidaAte { get; set; }

        [StringLength(2000, ErrorMessage = "As orientações devem ter no máximo 2000 caracteres.")]
        public string Orientacoes { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe ao menos um medicamento.")]
        [MinLength(1, ErrorMessage = "Informe ao menos um medicamento.")]
        public List<CriarItemPrescricaoDTO> Itens { get; set; } = new();
    }

    public class CriarItemPrescricaoDTO
    {
        [Required(ErrorMessage = "O medicamento é obrigatório.")]
        [StringLength(200, MinimumLength = 2)]
        public string Medicamento { get; set; } = string.Empty;

        [Required(ErrorMessage = "A dosagem é obrigatória.")]
        [StringLength(120, ErrorMessage = "A dosagem deve ter no máximo 120 caracteres.")]
        public string Dosagem { get; set; } = string.Empty;

        [Required(ErrorMessage = "A frequência é obrigatória.")]
        [StringLength(120, ErrorMessage = "A frequência deve ter no máximo 120 caracteres.")]
        public string Frequencia { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "A duração deve ter no máximo 120 caracteres.")]
        public string Duracao { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "A via deve ter no máximo 60 caracteres.")]
        public string Via { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "A observação deve ter no máximo 500 caracteres.")]
        public string Observacao { get; set; } = string.Empty;
    }

    public class PrescricaoDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public string Raca { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;
        public string NomeVeterinario { get; set; } = string.Empty;
        public string Crmv { get; set; } = string.Empty;
        public DateTime DataEmissao { get; set; }
        [DataDeCalendario]
        public DateTime? ValidaAte { get; set; }
        public string Orientacoes { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<ItemPrescricaoDTO> Itens { get; set; } = new();
    }

    public class ItemPrescricaoDTO
    {
        public Guid Id { get; set; }
        public string Medicamento { get; set; } = string.Empty;
        public string Dosagem { get; set; } = string.Empty;
        public string Frequencia { get; set; } = string.Empty;
        public string Duracao { get; set; } = string.Empty;
        public string Via { get; set; } = string.Empty;
        public string Observacao { get; set; } = string.Empty;
    }
}
