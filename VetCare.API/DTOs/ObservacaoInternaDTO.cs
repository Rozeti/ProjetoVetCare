namespace VetCare.API.DTOs
{
    public class ObservacaoInternaDTO
    {
        public Guid Id { get; set; }
        public Guid ProntuarioId { get; set; }
        public Guid? AvaliacaoId { get; set; }
        public Guid? AtendimentoId { get; set; }
        public string Autor { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public DateTime DataRegistro { get; set; }
    }
}
