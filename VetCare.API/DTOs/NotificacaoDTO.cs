namespace VetCare.API.DTOs
{
    public class NotificacaoDTO
    {
        public Guid Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string? LinkRelacionado { get; set; }
        public DateTime DataCriacao { get; set; }
        public bool Visualizada { get; set; }
    }
}
