using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-010: foto ou vídeo anexado a uma sessão.</summary>
    public class UploadMidiaDTO
    {
        [Required(ErrorMessage = "A sessão é obrigatória.")]
        public Guid SessaoId { get; set; }

        public Guid? AtendimentoId { get; set; }

        [Required(ErrorMessage = "Nenhum arquivo foi enviado.")]
        public IFormFile? Arquivo { get; set; }
    }
}
