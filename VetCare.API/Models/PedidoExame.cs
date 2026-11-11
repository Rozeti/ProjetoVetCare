namespace VetCare.API.Models
{
    /// <summary>
    /// Pedido de exames feito em uma avaliação ou em um tratamento (direto no tratamento ou no
    /// registro de uma das sessões dele). Quem o emitiu pode corrigi-lo (a versão anterior fica
    /// guardada) ou excluí-lo: a exclusão tira o pedido do prontuário, e a auditoria registra quem
    /// excluiu e o que estava pedido.
    /// </summary>
    public class PedidoExame
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid ProntuarioId { get; set; }

        /// <summary>
        /// Avaliação em que os exames foram pedidos; vazio no pedido feito no tratamento ou em uma
        /// das sessões dele, cuja origem é o tratamento.
        /// </summary>
        public Guid? AvaliacaoId { get; set; }

        /// <summary>Sessão registrada (atendimento) em que o pedido foi feito, quando foi feito nela.</summary>
        public Guid? AtendimentoId { get; set; }

        /// <summary>
        /// Tratamento do pedido. No pedido feito no tratamento ou em uma sessão é a origem dele; no
        /// de uma avaliação é um vínculo opcional.
        /// </summary>
        public Guid? TratamentoId { get; set; }

        public Guid VeterinarioId { get; set; }

        /// <summary>Exames solicitados, um por linha.</summary>
        public string Exames { get; set; } = string.Empty;

        /// <summary>Justificativa clínica ou orientações para o laboratório.</summary>
        public string Justificativa { get; set; } = string.Empty;

        public DateTime DataEmissao { get; set; } = DateTime.UtcNow;

        /// <summary>Ativo; "Cancelado" só aparece nos pedidos antigos, de antes da exclusão.</summary>
        public string Status { get; set; } = StatusPedidoExame.Ativo;

        public string MotivoCancelamento { get; set; } = string.Empty;

        public Pet? Paciente { get; set; }
        public Prontuario? Prontuario { get; set; }
        public AvaliacaoClinica? Avaliacao { get; set; }
        public AtendimentoFisioterapeutico? Atendimento { get; set; }
        public Tratamento? Tratamento { get; set; }
        public Veterinario? Veterinario { get; set; }
    }

    /// <summary>
    /// Estados de um pedido de exame. Hoje um pedido que não serve mais é excluído; o estado
    /// Cancelado continua existindo para os pedidos cancelados antes dessa mudança.
    /// </summary>
    public static class StatusPedidoExame
    {
        public const string Ativo = "Ativo";
        public const string Cancelado = "Cancelado";
    }
}
