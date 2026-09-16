namespace VetCare.API.DTOs
{
    public class ProntuarioDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public string Raca { get; set; } = string.Empty;
        public string Sexo { get; set; } = string.Empty;
        public string Pelagem { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public bool Castrado { get; set; }
        public DateTime DataNascimento { get; set; }
        public int IdadeAnos { get; set; }
        public string IdadeDescritiva { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;
        public string TelefoneTutor { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public DateTime UltimaAtualizacao { get; set; }
        public DateTime? DataObito { get; set; }

        /// <summary>Indica se o solicitante enxerga observações internas (RN-003).</summary>
        public bool ExibeObservacoesInternas { get; set; }

        /// <summary>Alergias e comorbidades ativas, destacadas antes de qualquer conduta.</summary>
        public List<AlergiaDTO> AlertasClinicos { get; set; } = new();

        public List<ItemLinhaTempoDTO> Historico { get; set; } = new();

        /// <summary>HU-011, CA-3: série usada no gráfico de evolução de peso.</summary>
        public List<PontoEvolucaoDTO> EvolucaoPeso { get; set; } = new();

        public List<PontoEvolucaoDTO> EvolucaoDor { get; set; } = new();

        public List<VacinaDTO> Vacinas { get; set; } = new();
        public List<PrescricaoDTO> Prescricoes { get; set; } = new();
        public List<DocumentoDTO> Documentos { get; set; } = new();
        public List<TratamentoDTO> Tratamentos { get; set; } = new();
    }
}
