using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarProntuarioDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }
    }
}
