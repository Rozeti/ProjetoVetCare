using VetCare.API.Common;

namespace VetCare.API.DTOs
{
    public class SessaoDTO
    {
        public Guid Id { get; set; }
        public Guid TratamentoId { get; set; }
        public string NomeTratamento { get; set; } = string.Empty;
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public Guid PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public string NomeTutor { get; set; } = string.Empty;

        /// <summary>Início da sessão.</summary>
        public DateTime DataHora { get; set; }

        /// <summary>Fim da sessão.</summary>
        public DateTime DataHoraFim { get; set; }

        public string Status { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;

        /// <summary>Sessão retirada do plano; continua no histórico e pode ser reativada.</summary>
        public bool Inativa { get; set; }

        /// <summary>Situação de leitura da sessão: Agendada, Aguardando registro, Realizada, Cancelada ou Inativada.</summary>
        public string Situacao { get; set; } = string.Empty;

        /// <summary>Indica se já existe atendimento registrado para esta sessão (HU-008).</summary>
        public bool PossuiAtendimento { get; set; }

        /// <summary>Registro do atendimento, quando existe: número da sessão no tratamento, dor e procedimentos.</summary>
        public Guid? AtendimentoId { get; set; }
        public int? NumeroSessao { get; set; }
        public int? EscalaDor { get; set; }
        public string TecnicasAplicadas { get; set; } = string.Empty;

        /// <summary>RN-009: informa à interface se o tutor ainda está dentro do prazo de cancelamento.</summary>
        public bool PodeCancelar { get; set; }
    }

    /// <summary>Tela de gestão das sessões: a página filtrada e quantas sessões há em cada situação.</summary>
    public class GestaoDeSessoesDTO
    {
        public PaginaDe<SessaoDTO> Pagina { get; set; } = PaginaDe<SessaoDTO>.Criar(Array.Empty<SessaoDTO>(), 1, 20, 0);

        /// <summary>Contagem por situação (Agendada, Aguardando registro...), com os mesmos filtros, menos o de situação.</summary>
        public Dictionary<string, int> Contagens { get; set; } = new();
    }
}
