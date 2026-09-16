namespace VetCare.API.Models
{
    /// <summary>
    /// HU-012: contratos, exames externos e laudos vinculados ao prontuário.
    /// Apenas os metadados vivem no banco; o binário fica no armazenamento de arquivos.
    /// </summary>
    public class DocumentoClinico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProntuarioId { get; set; }
        public Guid EnviadoPorId { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;
        public string UrlArquivo { get; set; } = string.Empty;
        public long TamanhoBytes { get; set; }
        public DateTime DataUpload { get; set; } = DateTime.UtcNow;

        public Prontuario? Prontuario { get; set; }
        public Usuario? EnviadoPor { get; set; }
    }
}
