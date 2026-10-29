namespace VetCare.API.DTOs
{
    /// <summary>
    /// Evento da linha do tempo clínica do animal (HU-011, CA-1): avaliações, início e
    /// encerramento de tratamentos, evoluções, cirurgias, vacinações, receitas e pedidos de exame.
    /// As observações internas só são preenchidas para Administrador e Veterinário (RN-003).
    /// </summary>
    public class ItemLinhaTempoDTO
    {
        /// <summary>Identificador do registro de origem (avaliação, tratamento, evolução...).</summary>
        public Guid Id { get; set; }

        /// <summary>Única por evento: um mesmo tratamento gera o evento de início e o de encerramento.</summary>
        public string Chave { get; set; } = string.Empty;

        public DateTime Data { get; set; }

        /// <summary>Avaliação, Tratamento, Evolução, Cirurgia, Vacinação, Receita, Exame ou Observação.</summary>
        public string Categoria { get; set; } = string.Empty;

        /// <summary>Título do evento, por exemplo "Avaliação Clínica" ou "Tratamento iniciado".</summary>
        public string Tipo { get; set; } = string.Empty;

        /// <summary>Responsável pelo registro, quando houver.</summary>
        public string Autor { get; set; } = string.Empty;

        /// <summary>Descrição resumida.</summary>
        public string Descricao { get; set; } = string.Empty;

        public string Detalhes { get; set; } = string.Empty;
        public bool Editado { get; set; }

        /// <summary>Tratamento ao qual o evento pertence (evoluções e eventos de tratamento).</summary>
        public Guid? TratamentoId { get; set; }

        /// <summary>Preenchido nas evoluções.</summary>
        public int? NumeroSessao { get; set; }
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
