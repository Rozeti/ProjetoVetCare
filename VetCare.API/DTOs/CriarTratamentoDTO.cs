using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarTratamentoDTO
    {
        [Required(ErrorMessage = "O paciente é obrigatório.")]
        public Guid PacienteId { get; set; }

        /// <summary>Opcional para o veterinário logado: assume o próprio vínculo quando ausente.</summary>
        public Guid? VeterinarioId { get; set; }

        /// <summary>Sem valor, o tratamento começa no momento do cadastro; nunca no futuro.</summary>
        public DateTime DataInicio { get; set; }

        [Required(ErrorMessage = "O objetivo terapêutico é obrigatório.")]
        [StringLength(1000, MinimumLength = 3, ErrorMessage = "O objetivo terapêutico deve ter entre 3 e 1000 caracteres.")]
        public string ObjetivoTerapeutico { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "As observações devem ter no máximo 2000 caracteres.")]
        public string ObservacoesGerais { get; set; } = string.Empty;
    }
}
