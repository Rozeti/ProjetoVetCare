using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarTratamentoDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        /// <summary>Opcional para o veterinário logado: assume o próprio vínculo quando ausente.</summary>
        public Guid? VeterinarioId { get; set; }

        public DateTime DataInicio { get; set; }

        [Required(ErrorMessage = "O objetivo terapêutico é obrigatório.")]
        public string ObjetivoTerapeutico { get; set; } = string.Empty;

        public string ObservacoesGerais { get; set; } = string.Empty;
    }
}
