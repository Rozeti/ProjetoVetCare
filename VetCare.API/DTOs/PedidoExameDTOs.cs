using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Pedido de exames feito em uma avaliação, em uma sessão registrada ou no tratamento: informe
    /// a avaliação, a sessão ou, sem as duas, o tratamento. O paciente vem da origem.
    /// </summary>
    public class CriarPedidoExameDTO
    {
        public Guid? AvaliacaoId { get; set; }

        /// <summary>Sessão registrada (atendimento) em que os exames foram pedidos.</summary>
        public Guid? AtendimentoId { get; set; }

        /// <summary>
        /// Tratamento do pedido. Sozinho, é a origem (pedido feito no tratamento); no pedido de uma
        /// sessão é sempre o da própria sessão (pode ser omitido); no de uma avaliação é um vínculo
        /// opcional.
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
    /// Correção do pedido: os exames, a justificativa e, no pedido de uma avaliação, a avaliação de
    /// origem e o tratamento vinculado. O pedido feito no tratamento ou em uma sessão continua
    /// ligado ao tratamento.
    /// </summary>
    public class AtualizarPedidoExameDTO
    {
        /// <summary>Opcional: outra avaliação do mesmo animal passa a ser a origem do pedido.</summary>
        public Guid? AvaliacaoId { get; set; }

        /// <summary>
        /// O tratamento do pedido de uma avaliação depois da correção; vazio, o pedido fica sem
        /// tratamento. Ignorado no pedido feito no tratamento ou em uma sessão, que não muda de tratamento.
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
        /// <summary>Avaliação em que o pedido foi feito; vazio no pedido feito no tratamento.</summary>
        public Guid? AvaliacaoId { get; set; }
        public DateTime? DataAvaliacao { get; set; }
        public string MotivoAvaliacao { get; set; } = string.Empty;

        /// <summary>Sessão registrada em que o pedido foi feito, quando foi feito nela.</summary>
        public Guid? AtendimentoId { get; set; }
        public int? NumeroSessao { get; set; }

        /// <summary>"Avaliação" ou "Tratamento" (também o pedido feito em uma sessão).</summary>
        public string TipoOrigem { get; set; } = string.Empty;

        /// <summary>Descrição da origem: "Avaliação de 12/04/2026" ou "Tratamento Hidroterapia".</summary>
        public string Origem { get; set; } = string.Empty;

        /// <summary>Tratamento do pedido (a origem, ou o vínculo do pedido de uma avaliação); vazio sem vínculo.</summary>
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
