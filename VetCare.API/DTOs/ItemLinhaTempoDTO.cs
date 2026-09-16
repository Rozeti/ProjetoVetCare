namespace VetCare.API.DTOs
{
    /// <summary>
    /// Entrada da linha do tempo cronológica do prontuário (HU-011, CA-1).
    /// As observações internas só são preenchidas para Administrador e Veterinário (RN-003).
    /// </summary>
    public class ItemLinhaTempoDTO
    {
        public Guid Id { get; set; }
        public DateTime Data { get; set; }

        /// <summary>Avaliação Clínica ou Atendimento.</summary>
        public string Tipo { get; set; } = string.Empty;

        public string Autor { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Detalhes { get; set; } = string.Empty;
        public bool Editado { get; set; }

        /// <summary>Preenchido apenas em itens do tipo Atendimento.</summary>
        public int? EscalaDor { get; set; }
        public decimal? PesoKg { get; set; }

        /// <summary>Campos completos do registro, para a visão expandida.</summary>
        public Dictionary<string, string> Campos { get; set; } = new();

        /// <summary>HU-011, CA-4: mídias exibidas na posição correspondente da linha do tempo.</summary>
        public List<MidiaDTO> Midias { get; set; } = new();

        /// <summary>RN-003: sempre vazio quando o solicitante é Tutor.</summary>
        public List<ObservacaoInternaDTO> ObservacoesInternas { get; set; } = new();
    }
}
