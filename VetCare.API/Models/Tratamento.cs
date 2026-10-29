namespace VetCare.API.Models
{
    /// <summary>
    /// Procedimento ou plano terapêutico encaminhado a partir de uma avaliação. Não existe sem
    /// a avaliação de origem; cada sessão realizada gera uma evolução vinculada a ele.
    /// </summary>
    public class Tratamento
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid AvaliacaoId { get; set; }
        public Guid VeterinarioId { get; set; }

        public string Nome { get; set; } = string.Empty;

        /// <summary>Modalidade terapêutica: fisioterapia, RPG, hidroterapia, acupuntura...</summary>
        public string Tipo { get; set; } = string.Empty;

        public string Descricao { get; set; } = string.Empty;

        /// <summary>Texto livre, por exemplo "3 meses".</summary>
        public string DuracaoPrevista { get; set; } = string.Empty;

        /// <summary>Texto livre, por exemplo "1 sessão por semana".</summary>
        public string Frequencia { get; set; } = string.Empty;

        public int SessoesPrevistas { get; set; }

        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }

        /// <summary>Planejado, Em Andamento, Concluído ou Inativado.</summary>
        public string Status { get; set; } = StatusTratamento.Planejado;

        public string ObservacoesGerais { get; set; } = string.Empty;

        public Pet? Paciente { get; set; }
        public AvaliacaoClinica? Avaliacao { get; set; }
        public Veterinario? Veterinario { get; set; }
        public ICollection<Sessao> Sessoes { get; set; } = new List<Sessao>();
    }
}
