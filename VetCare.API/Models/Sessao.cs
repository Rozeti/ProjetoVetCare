namespace VetCare.API.Models
{
    public class Sessao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TratamentoId { get; set; }
        public Guid VeterinarioId { get; set; }
        public DateTime DataHora { get; set; }

        /// <summary>Aguardando confirmação, Confirmada, Cancelada ou Concluída.</summary>
        public string Status { get; set; } = "Aguardando confirmação";
        public string Observacoes { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        /// <summary>Marca o envio do lembrete de confirmação para não notificar em duplicidade (HU-015, CA-2).</summary>
        public bool LembreteEnviado { get; set; }

        public Tratamento? Tratamento { get; set; }
        public Veterinario? Veterinario { get; set; }
    }
}
