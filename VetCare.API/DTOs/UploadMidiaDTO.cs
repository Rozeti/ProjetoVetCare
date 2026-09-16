namespace VetCare.API.DTOs
{
    public class UploadMidiaDTO
    {
        public Guid SessaoId { get; set; }
        public Guid? AtendimentoId { get; set; }
        public IFormFile? Arquivo { get; set; }
    }
}
