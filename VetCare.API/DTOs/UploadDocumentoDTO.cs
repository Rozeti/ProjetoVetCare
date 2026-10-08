using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-012: anexo de contratos, exames externos e laudos.</summary>
    public class UploadDocumentoDTO
    {
        public Guid? ProntuarioId { get; set; }
        public Guid? PacienteId { get; set; }

        /// <summary>Contrato, Exame, Laudo ou Outro.</summary>
        [StringLength(40, ErrorMessage = "O tipo do documento deve ter até 40 caracteres.")]
        public string TipoDocumento { get; set; } = "Outro";

        [Required(ErrorMessage = "Nenhum arquivo foi enviado.")]
        public IFormFile? Arquivo { get; set; }
    }
}
