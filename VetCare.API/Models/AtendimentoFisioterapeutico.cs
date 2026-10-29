namespace VetCare.API.Models
{
    /// <summary>
    /// Evolução: o registro de uma sessão realizada dentro de um tratamento. Diz o que foi feito,
    /// como o animal reagiu e pode gerar uma receita. O nome da classe vem da primeira versão do
    /// sistema, em que o registro se chamava "atendimento fisioterapêutico".
    /// </summary>
    public class AtendimentoFisioterapeutico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessaoId { get; set; }
        public Guid TratamentoId { get; set; }
        public Guid ProntuarioId { get; set; }
        public Guid VeterinarioId { get; set; }

        /// <summary>Posição da evolução no tratamento: 1 para a primeira sessão registrada, e assim por diante.</summary>
        public int NumeroSessao { get; set; }

        /// <summary>Procedimentos realizados na sessão.</summary>
        public string TecnicasAplicadas { get; set; } = string.Empty;

        /// <summary>Escala de dor de 0 a 10 (HU-008, CA-2).</summary>
        public int EscalaDor { get; set; }

        /// <summary>Descrição do que foi realizado na sessão.</summary>
        public string EvolucaoClinica { get; set; } = string.Empty;

        /// <summary>Como o animal reagiu durante a sessão.</summary>
        public string ReacaoAnimal { get; set; } = string.Empty;

        public string Observacoes { get; set; } = string.Empty;
        public string Relatorio { get; set; } = string.Empty;
        public string SinaisVitais { get; set; } = string.Empty;
        public string ProximosPassos { get; set; } = string.Empty;

        /// <summary>Alimenta o gráfico de evolução de peso do prontuário (HU-011, CA-3).</summary>
        public decimal? PesoKg { get; set; }
        public decimal? TemperaturaCelsius { get; set; }
        public int? FrequenciaCardiaca { get; set; }
        public int? FrequenciaRespiratoria { get; set; }

        public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
        public DateTime? DataUltimaEdicao { get; set; }

        public Sessao? Sessao { get; set; }
        public Tratamento? Tratamento { get; set; }
        public Prontuario? Prontuario { get; set; }
        public Veterinario? Veterinario { get; set; }
    }
}
