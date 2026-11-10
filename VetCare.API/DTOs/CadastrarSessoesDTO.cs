using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// HU-004: cadastro das sessões de um tratamento, uma ou várias de uma só vez. É o único
    /// caminho para criar sessões; mais sessões podem ser cadastradas depois, no mesmo tratamento.
    /// </summary>
    public class CadastrarSessoesDTO
    {
        /// <summary>Quantas sessões um mesmo cadastro aceita.</summary>
        public const int MaximoPorCadastro = 60;

        [Required(ErrorMessage = "O tratamento é obrigatório.")]
        public Guid TratamentoId { get; set; }

        /// <summary>Opcional para o veterinário logado: assume o próprio vínculo quando ausente.</summary>
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "Informe ao menos uma sessão, com o início e o fim.")]
        [MinLength(1, ErrorMessage = "Informe ao menos uma sessão, com o início e o fim.")]
        [MaxLength(MaximoPorCadastro, ErrorMessage = "Cadastre até 60 sessões por vez.")]
        public List<HorarioDeSessaoDTO> Sessoes { get; set; } = new();

        /// <summary>Orientações ao tutor, repetidas em todas as sessões do cadastro.</summary>
        [StringLength(1000, ErrorMessage = "As observações devem ter até 1000 caracteres.")]
        public string Observacoes { get; set; } = string.Empty;
    }

    /// <summary>Início e fim de uma sessão; os dois são obrigatórios.</summary>
    public class HorarioDeSessaoDTO
    {
        [Required(ErrorMessage = "Informe a data e o horário de início de cada sessão.")]
        public DateTime? Inicio { get; set; }

        [Required(ErrorMessage = "Informe o horário de término de cada sessão.")]
        public DateTime? Fim { get; set; }
    }

    /// <summary>
    /// Plano de sessões de um tratamento: quantas, a partir de quando, em quais dias da semana e em
    /// que horário. A API monta as datas, pulando as que não cabem na agenda do veterinário, e
    /// devolve a prévia; o cadastro em si recebe essas datas em <see cref="CadastrarSessoesDTO"/>.
    /// </summary>
    public class PlanoDeSessoesDTO
    {
        /// <summary>Até onde a prévia procura datas livres.</summary>
        public const int DiasProcurados = 366;

        [Required(ErrorMessage = "O tratamento é obrigatório.")]
        public Guid TratamentoId { get; set; }

        /// <summary>Opcional para o veterinário logado: assume o próprio vínculo quando ausente.</summary>
        public Guid? VeterinarioId { get; set; }

        [Range(1, CadastrarSessoesDTO.MaximoPorCadastro, ErrorMessage = "Informe de 1 a 60 sessões.")]
        public int Quantidade { get; set; }

        /// <summary>Primeiro dia em que as sessões podem acontecer (data da clínica).</summary>
        [Required(ErrorMessage = "Informe a partir de qual data as sessões começam.")]
        public DateTime? APartirDe { get; set; }

        /// <summary>Horário de início, "HH:mm", no horário da clínica.</summary>
        [Required(ErrorMessage = "Informe o horário de início das sessões.")]
        public string? HoraInicio { get; set; }

        /// <summary>Horário de término, "HH:mm", no horário da clínica.</summary>
        [Required(ErrorMessage = "Informe o horário de término das sessões.")]
        public string? HoraFim { get; set; }

        /// <summary>Dias da semana das sessões: 0 = domingo ... 6 = sábado.</summary>
        [Required(ErrorMessage = "Escolha ao menos um dia da semana.")]
        [MinLength(1, ErrorMessage = "Escolha ao menos um dia da semana.")]
        public List<int> DiasDaSemana { get; set; } = new();
    }

    /// <summary>As datas que o plano gera, as que ficaram de fora e o efeito no total do tratamento.</summary>
    public class PreviaDeSessoesDTO
    {
        public List<HorarioDeSessaoDTO> Sessoes { get; set; } = new();

        /// <summary>Dias do plano em que a sessão não cabia na agenda, com o motivo.</summary>
        public List<DataPuladaDTO> Puladas { get; set; } = new();

        /// <summary>Sessões previstas no tratamento hoje.</summary>
        public int SessoesPrevistas { get; set; }

        /// <summary>Sessões do plano já cadastradas (sem as canceladas e as inativadas).</summary>
        public int SessoesCadastradas { get; set; }

        /// <summary>Quantas sessões o tratamento passa a prever: cresce quando o plano é ampliado.</summary>
        public int NovoTotalPrevisto { get; set; }
    }

    public class DataPuladaDTO
    {
        public DateTime Data { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }

    /// <summary>Retira uma sessão do plano; o motivo vai para as observações e para a auditoria.</summary>
    public class InativarSessaoDTO
    {
        [StringLength(300, ErrorMessage = "O motivo deve ter até 300 caracteres.")]
        public string? Motivo { get; set; }
    }
}
