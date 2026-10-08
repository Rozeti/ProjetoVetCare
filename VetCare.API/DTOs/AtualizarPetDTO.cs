using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarPetDTO
    {
        [Required(ErrorMessage = "O nome do paciente é obrigatório.")]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "O nome deve ter até 80 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "A espécie é obrigatória.")]
        [StringLength(40, ErrorMessage = "A espécie deve ter até 40 caracteres.")]
        public string Especie { get; set; } = string.Empty;

        [StringLength(80, ErrorMessage = "A raça deve ter até 80 caracteres.")]
        public string Raca { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
        public DateTime DataNascimento { get; set; }

        [StringLength(20, ErrorMessage = "O sexo deve ter até 20 caracteres.")]
        public string Sexo { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "A pelagem deve ter até 60 caracteres.")]
        public string Pelagem { get; set; } = string.Empty;

        [StringLength(40, ErrorMessage = "O microchip deve ter até 40 caracteres.")]
        public string Microchip { get; set; } = string.Empty;

        public bool Castrado { get; set; }

        [Range(0.1, 200, ErrorMessage = "Informe um peso entre 0,1 e 200 kg.")]
        public decimal? PesoAtualKg { get; set; }

        /// <summary>Registrado quando o paciente vem a óbito; encerra os tratamentos em aberto. Nulo desfaz o registro.</summary>
        public DateTime? DataObito { get; set; }

        /// <summary>RN-001: a reatribuição de tutor preserva todo o histórico clínico.</summary>
        [Required(ErrorMessage = "O paciente precisa estar vinculado a um tutor.")]
        public Guid TutorId { get; set; }
    }
}
