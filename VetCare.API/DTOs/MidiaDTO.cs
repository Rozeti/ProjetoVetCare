namespace VetCare.API.DTOs
{
    public class MidiaDTO
    {
        public Guid Id { get; set; }
        public Guid SessaoId { get; set; }
        public Guid? AtendimentoId { get; set; }

        /// <summary>Imagem ou Video.</summary>
        public string Tipo { get; set; } = string.Empty;

        public string NomeArquivo { get; set; } = string.Empty;
        public string UrlArquivo { get; set; } = string.Empty;
        public DateTime DataUpload { get; set; }
    }
}
