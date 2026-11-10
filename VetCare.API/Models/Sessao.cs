namespace VetCare.API.Models
{
    /// <summary>
    /// Sessão de um tratamento, cadastrada com início e fim. Quando realizada, recebe o registro
    /// do atendimento (<see cref="AtendimentoFisioterapeutico"/>), que entra no prontuário.
    /// </summary>
    public class Sessao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TratamentoId { get; set; }
        public Guid VeterinarioId { get; set; }

        /// <summary>Início da sessão.</summary>
        public DateTime DataHora { get; set; }

        /// <summary>Fim da sessão: com o início, define o horário que ela ocupa na agenda (RN-002).</summary>
        public DateTime DataHoraFim { get; set; }

        /// <summary>Aguardando confirmação, Confirmada, Cancelada ou Concluída.</summary>
        public string Status { get; set; } = StatusSessao.AguardandoConfirmacao;
        public string Observacoes { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sessão retirada do plano (cadastrada por engano ou que deixou de ser necessária). Sai da
        /// agenda, das contagens e da visão do tutor, mas continua no histórico e pode ser reativada.
        /// </summary>
        public bool Inativa { get; set; }

        /// <summary>Marca o envio do lembrete de confirmação para não notificar em duplicidade (HU-015, CA-2).</summary>
        public bool LembreteEnviado { get; set; }

        public Tratamento? Tratamento { get; set; }
        public Veterinario? Veterinario { get; set; }
    }
}
