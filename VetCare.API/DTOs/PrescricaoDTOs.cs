using System.ComponentModel.DataAnnotations;
using VetCare.API.Common;

namespace VetCare.API.DTOs
{
    public class CriarPrescricaoDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        public Guid? VeterinarioId { get; set; }

        /// <summary>
        /// Origem da receita: a avaliação ou a sessão registrada (atendimento) em que foi
        /// prescrita. Sem as duas, a origem é o tratamento informado em <see cref="TratamentoId"/>.
        /// </summary>
        public Guid? AvaliacaoId { get; set; }

        public Guid? AtendimentoId { get; set; }

        /// <summary>
        /// Tratamento da receita. Sozinho, é a origem (receita emitida no tratamento); na receita
        /// de uma sessão é sempre o da própria sessão (pode ser omitido); na de uma avaliação é um
        /// vínculo opcional.
        /// </summary>
        public Guid? TratamentoId { get; set; }

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

        public Guid? AvaliacaoId { get; set; }
        public DateTime? DataAvaliacao { get; set; }
        public string MotivoAvaliacao { get; set; } = string.Empty;
        public Guid? AtendimentoId { get; set; }

        /// <summary>"Avaliação" ou "Tratamento" (também a receita feita em uma sessão); vazio nas antigas sem origem.</summary>
        public string TipoOrigem { get; set; } = string.Empty;

        /// <summary>Descrição da origem: "Avaliação de 12/04/2026" ou "Tratamento Hidroterapia".</summary>
        public string Origem { get; set; } = string.Empty;

        /// <summary>Tratamento da receita (a origem, ou o vínculo da receita de uma avaliação); vazio sem vínculo.</summary>
        public Guid? TratamentoId { get; set; }
        public string NomeTratamento { get; set; } = string.Empty;

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
