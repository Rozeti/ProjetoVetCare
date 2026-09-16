using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class EnviarMensagemDTO
    {
        [Required(ErrorMessage = "O destinatário é obrigatório.")]
        public Guid DestinatarioId { get; set; }

        /// <summary>Opcional: associa a conversa a um paciente específico.</summary>
        public Guid? PacienteId { get; set; }

        [Required(ErrorMessage = "A mensagem não pode ficar vazia.")]
        [StringLength(2000, MinimumLength = 1)]
        public string Conteudo { get; set; } = string.Empty;
    }
}
