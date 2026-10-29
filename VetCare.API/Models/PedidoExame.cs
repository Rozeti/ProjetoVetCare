namespace VetCare.API.Models
{
    /// <summary>
    /// Pedido de exames gerado a partir de uma avaliação, com ou sem tratamento. Como a receita,
    /// é cancelado e nunca apagado (RN-004).
    /// </summary>
    public class PedidoExame
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid ProntuarioId { get; set; }
        public Guid AvaliacaoId { get; set; }
        public Guid VeterinarioId { get; set; }

        /// <summary>Exames solicitados, um por linha.</summary>
        public string Exames { get; set; } = string.Empty;

        /// <summary>Justificativa clínica ou orientações para o laboratório.</summary>
        public string Justificativa { get; set; } = string.Empty;

        public DateTime DataEmissao { get; set; } = DateTime.UtcNow;

        /// <summary>Ativo ou Cancelado.</summary>
        public string Status { get; set; } = StatusPedidoExame.Ativo;

        public string MotivoCancelamento { get; set; } = string.Empty;

        public Pet? Paciente { get; set; }
        public Prontuario? Prontuario { get; set; }
        public AvaliacaoClinica? Avaliacao { get; set; }
        public Veterinario? Veterinario { get; set; }
    }

    /// <summary>Estados de um pedido de exame. RN-004: ele é cancelado, nunca apagado.</summary>
    public static class StatusPedidoExame
    {
        public const string Ativo = "Ativo";
        public const string Cancelado = "Cancelado";
    }
}
