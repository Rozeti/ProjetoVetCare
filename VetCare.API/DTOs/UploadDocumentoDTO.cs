namespace VetCare.API.DTOs
{
    /// <summary>HU-012: anexo de contratos, exames externos e laudos.</summary>
    public class UploadDocumentoDTO
    {
        public Guid? ProntuarioId { get; set; }
        public Guid? PacienteId { get; set; }

        /// <summary>Contrato, Exame, Laudo ou Outro.</summary>
        public string TipoDocumento { get; set; } = "Outro";

        public IFormFile? Arquivo { get; set; }
    }
}
