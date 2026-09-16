namespace VetCare.API.Models
{
    /// <summary>
    /// RN-004: registros clínicos confirmados não podem ser excluídos e toda alteração
    /// preserva a rastreabilidade da versão anterior. Cada edição arquiva aqui o
    /// conteúdo que existia antes da mudança.
    /// </summary>
    public class VersaoRegistroClinico
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>AvaliacaoClinica ou AtendimentoFisioterapeutico.</summary>
        public string TipoRegistro { get; set; } = string.Empty;
        public Guid RegistroId { get; set; }

        /// <summary>Snapshot em JSON do estado anterior do registro.</summary>
        public string ConteudoAnterior { get; set; } = string.Empty;
        public Guid AlteradoPorId { get; set; }
        public DateTime DataAlteracao { get; set; } = DateTime.UtcNow;

        public Usuario? AlteradoPor { get; set; }
    }
}
