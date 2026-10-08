using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>RN-004: a edição preserva a versão anterior do registro.</summary>
    public class AtualizarAtendimentoDTO
    {
        [Required(ErrorMessage = "Informe as técnicas aplicadas.")]
        [StringLength(1000, ErrorMessage = "As técnicas aplicadas devem ter até 1000 caracteres.")]
        public string TecnicasAplicadas { get; set; } = string.Empty;

        [Range(0, 10, ErrorMessage = "A escala de dor deve ser um valor entre 0 e 10.")]
        public int EscalaDor { get; set; }

        [Required(ErrorMessage = "A evolução clínica é obrigatória.")]
        [StringLength(4000, ErrorMessage = "A evolução clínica deve ter até 4000 caracteres.")]
        public string EvolucaoClinica { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Os sinais vitais devem ter até 500 caracteres.")]
        public string SinaisVitais { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Os próximos passos devem ter até 2000 caracteres.")]
        public string ProximosPassos { get; set; } = string.Empty;

        [Range(0.1, 200, ErrorMessage = "Informe um peso entre 0,1 e 200 kg.")]
        public decimal? PesoKg { get; set; }

        [Range(30, 45, ErrorMessage = "Informe uma temperatura entre 30 e 45 graus.")]
        public decimal? TemperaturaCelsius { get; set; }

        [Range(10, 400, ErrorMessage = "Informe uma frequência cardíaca entre 10 e 400 bpm.")]
        public int? FrequenciaCardiaca { get; set; }

        [Range(5, 200, ErrorMessage = "Informe uma frequência respiratória entre 5 e 200 mpm.")]
        public int? FrequenciaRespiratoria { get; set; }
    }
}
