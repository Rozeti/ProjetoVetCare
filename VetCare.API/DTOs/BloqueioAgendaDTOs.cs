using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarBloqueioAgendaDTO
    {
        /// <summary>Opcional para o veterinário logado: assume o próprio vínculo quando ausente.</summary>
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe o início do bloqueio.")]
        public DateTime Inicio { get; set; }

        [Required(ErrorMessage = "Informe o fim do bloqueio.")]
        public DateTime Fim { get; set; }

        [Required(ErrorMessage = "Informe o motivo do bloqueio.")]
        [StringLength(200, MinimumLength = 3)]
        public string Motivo { get; set; } = string.Empty;
    }

    public class BloqueioAgendaDTO
    {
        public Guid Id { get; set; }
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
    }
}
