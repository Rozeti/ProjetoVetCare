namespace VetCare.API.Models
{
    public class MidiaSessao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessaoId { get; set; }
        public Guid? AtendimentoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string NomeArquivo { get; set; } = string.Empty;
        public string UrlArquivo { get; set; } = string.Empty;
        public DateTime DataUpload { get; set; } = DateTime.UtcNow;

        public Sessao? Sessao { get; set; }
        public AtendimentoFisioterapeutico? Atendimento { get; set; }
    }
}