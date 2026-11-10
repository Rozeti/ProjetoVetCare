using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Pedido de exames gerado a partir de uma avaliação ou de uma sessão registrada: informe
    /// exatamente uma das duas origens. O paciente vem da origem.
    /// </summary>
    public class CriarPedidoExameDTO
    {
        public Guid? AvaliacaoId { get; set; }

        /// <summary>Sessão registrada (atendimento) em que os exames foram pedidos.</summary>
        public Guid? AtendimentoId { get; set; }

        /// <summary>
        /// Tratamento para o qual os exames são pedidos. No pedido de uma sessão é sempre o da
        /// própria sessão (pode ser omitido); no de uma avaliação é opcional.
        /// </summary>
        public Guid? TratamentoId { get; set; }

        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe os exames solicitados.")]
        [StringLength(2000, MinimumLength = 2, ErrorMessage = "Os exames solicitados devem ter entre 2 e 2000 caracteres.")]
        public string Exames { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "A justificativa deve ter no máximo 2000 caracteres.")]
        public string Justificativa { get; set; } = string.Empty;
    }

    /// <summary>
    /// Correção do pedido: os exames, a justificativa, o tratamento e, no pedido de uma avaliação,
    /// a avaliação de origem. O pedido feito em uma sessão continua ligado a ela e ao tratamento dela.
    /// </summary>
    public class AtualizarPedidoExameDTO
    {
        /// <summary>Opcional: outra avaliação do mesmo animal passa a ser a origem do pedido.</summary>
        public Guid? AvaliacaoId { get; set; }

        /// <summary>
        /// O tratamento do pedido depois da correção; vazio, o pedido fica sem tratamento. Ignorado
        /// no pedido de uma sessão, que fica sempre com o tratamento da sessão.
        /// </summary>
        public Guid? TratamentoId { get; set; }

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

        /// <summary>Sessão registrada em que o pedido foi feito; vazio no pedido da avaliação.</summary>
        public Guid? AtendimentoId { get; set; }
        public int? NumeroSessao { get; set; }

        /// <summary>"Avaliação de 12/04/2026" ou "Sessão nº 3 de 20/04/2026".</summary>
        public string Origem { get; set; } = string.Empty;

        /// <summary>Tratamento para o qual os exames foram pedidos; vazio quando não há vínculo.</summary>
        public Guid? TratamentoId { get; set; }
        public string NomeTratamento { get; set; } = string.Empty;

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
