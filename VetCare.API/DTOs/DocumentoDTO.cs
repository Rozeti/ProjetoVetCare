namespace VetCare.API.DTOs
{
    public class DocumentoDTO
    {
        public Guid Id { get; set; }
        public Guid ProntuarioId { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;
        public string UrlArquivo { get; set; } = string.Empty;
        public long TamanhoBytes { get; set; }
        public string EnviadoPor { get; set; } = string.Empty;
        public DateTime DataUpload { get; set; }
    }
}
