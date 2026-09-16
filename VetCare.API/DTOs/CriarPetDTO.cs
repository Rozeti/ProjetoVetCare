using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarPetDTO
    {
        [Required(ErrorMessage = "O nome do paciente é obrigatório.")]
        [StringLength(80, MinimumLength = 1)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "A espécie é obrigatória.")]
        public string Especie { get; set; } = string.Empty;

        public string Raca { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
        public DateTime DataNascimento { get; set; }

        public string Sexo { get; set; } = string.Empty;
        public string Pelagem { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public bool Castrado { get; set; }

        [Range(0.1, 200, ErrorMessage = "Informe um peso entre 0,1 e 200 kg.")]
        public decimal? PesoAtualKg { get; set; }

        /// <summary>RN-001: o vínculo com o tutor responsável é obrigatório.</summary>
        [Required(ErrorMessage = "O paciente precisa estar vinculado a um tutor.")]
        public Guid TutorId { get; set; }
    }
}
