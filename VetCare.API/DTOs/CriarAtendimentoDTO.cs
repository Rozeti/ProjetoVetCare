using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class CriarAtendimentoDTO
    {
        [Required(ErrorMessage = "A sessão é obrigatória.")]
        public Guid SessaoId { get; set; }

        /// <summary>Opcionais: derivados da sessão quando não informados.</summary>
        public Guid? TratamentoId { get; set; }
        public Guid? ProntuarioId { get; set; }
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe as técnicas aplicadas.")]
        public string TecnicasAplicadas { get; set; } = string.Empty;

        /// <summary>HU-008, CA-2: a escala de dor precisa ficar entre 0 e 10.</summary>
        [Range(0, 10, ErrorMessage = "A escala de dor deve ser um valor entre 0 e 10.")]
        public int EscalaDor { get; set; }

        [Required(ErrorMessage = "A evolução clínica é obrigatória.")]
        public string EvolucaoClinica { get; set; } = string.Empty;

        public string SinaisVitais { get; set; } = string.Empty;
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
        public string? ObservacaoInterna { get; set; }

        /// <summary>Conclui a sessão automaticamente ao salvar o atendimento.</summary>
        public bool ConcluirSessao { get; set; } = true;
    }
}
