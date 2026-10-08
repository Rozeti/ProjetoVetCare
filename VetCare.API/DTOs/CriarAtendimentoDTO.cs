using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>HU-008: o tratamento e o prontuário são deduzidos da própria sessão.</summary>
    public class CriarAtendimentoDTO
    {
        [Required(ErrorMessage = "A sessão é obrigatória.")]
        public Guid SessaoId { get; set; }

        /// <summary>Opcional: o veterinário autenticado, ou o da sessão, quando não informado.</summary>
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe as técnicas aplicadas.")]
        [StringLength(1000, ErrorMessage = "As técnicas aplicadas devem ter até 1000 caracteres.")]
        public string TecnicasAplicadas { get; set; } = string.Empty;

        /// <summary>HU-008, CA-2: a escala de dor precisa ficar entre 0 e 10.</summary>
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

        /// <summary>HU-009: observação restrita registrada junto com o atendimento.</summary>
        [StringLength(2000, ErrorMessage = "A observação interna deve ter até 2000 caracteres.")]
        public string? ObservacaoInterna { get; set; }

        /// <summary>Conclui a sessão ao salvar o atendimento; o tutor é avisado no mesmo aviso do registro.</summary>
        public bool ConcluirSessao { get; set; } = true;
    }
}
