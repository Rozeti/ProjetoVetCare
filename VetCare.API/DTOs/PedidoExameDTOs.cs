using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>Pedido de exames gerado a partir de uma avaliação; o paciente vem dela.</summary>
    public class CriarPedidoExameDTO
    {
        [Required(ErrorMessage = "A avaliação de origem é obrigatória.")]
        public Guid AvaliacaoId { get; set; }

        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe os exames solicitados.")]
        [StringLength(2000, MinimumLength = 2, ErrorMessage = "Os exames solicitados devem ter entre 2 e 2000 caracteres.")]
        public string Exames { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "A justificativa deve ter no máximo 2000 caracteres.")]
        public string Justificativa { get; set; } = string.Empty;
    }

    /// <summary>Correção do pedido: os exames, a justificativa e, se preciso, a avaliação de origem.</summary>
    public class AtualizarPedidoExameDTO
    {
        /// <summary>Opcional: outra avaliação do mesmo animal passa a ser a origem do pedido.</summary>
        public Guid? AvaliacaoId { get; set; }

        [Required(ErrorMessage = "Informe os exames solicitados.")]
        [StringLength(2000, MinimumLength = 2, ErrorMessage = "Os exames solicitados devem ter entre 2 e 2000 caracteres.")]
        public string Exames { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "A justificativa deve ter no máximo 2000 caracteres.")]
        public string Justificativa { get; set; } = string.Empty;
    }

    public class PedidoExameDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public string Raca { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;
        public Guid AvaliacaoId { get; set; }
        public DateTime? DataAvaliacao { get; set; }
        public string MotivoAvaliacao { get; set; } = string.Empty;

        /// <summary>Quem assinou: o veterinário só edita e exclui os próprios pedidos.</summary>
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string Crmv { get; set; } = string.Empty;
        public DateTime DataEmissao { get; set; }
        public string Exames { get; set; } = string.Empty;
        public string Justificativa { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string MotivoCancelamento { get; set; } = string.Empty;
    }
}
