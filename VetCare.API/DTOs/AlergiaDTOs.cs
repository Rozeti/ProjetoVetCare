using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarAlergiaDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        [Required(ErrorMessage = "Informe o tipo: Alergia, Comorbidade, Restricao ou Cirurgia.")]
        public string Tipo { get; set; } = "Alergia";

        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(400, MinimumLength = 3)]
        public string Descricao { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a gravidade: Leve, Moderada ou Grave.")]
        public string Gravidade { get; set; } = "Moderada";
    }

    public class AlergiaDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Gravidade { get; set; } = string.Empty;
        public string RegistradoPor { get; set; } = string.Empty;
        public DateTime DataRegistro { get; set; }
        public bool Ativa { get; set; }
    }
}
