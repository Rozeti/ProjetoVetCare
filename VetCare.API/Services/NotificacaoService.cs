using VetCare.API.Data;
using VetCare.API.Models;

namespace VetCare.API.Services
{
    /// <summary>
    /// Centraliza os gatilhos de notificação previstos na HU-015, para que os casos de
    /// uso apenas informem o evento ocorrido. Cada aviso é gravado no sistema e, em
    /// seguida, o <see cref="EntregadorDeNotificacoes"/> o envia por e-mail e push.
    /// </summary>
    public class NotificacaoService
    {
        /// <summary>Caminhos do portal para onde cada notificação leva; o aplicativo traduz para as próprias telas.</summary>
        public const string LinkAgendaDoTutor = "/minha-agenda";
        public const string LinkAgendaDoVeterinario = "/agenda";
        public const string LinkMensagens = "/mensagens";

        public static string LinkProntuario(Guid pacienteId) => $"/prontuario/{pacienteId}";

        private readonly INotificacaoRepository _repositorio;
        private readonly SinalDeNotificacoes _sinal;

        public NotificacaoService(INotificacaoRepository repositorio, SinalDeNotificacoes sinal)
        {
            _repositorio = repositorio;
            _sinal = sinal;
        }

        /// <summary>HU-015, CA-1: uma nova sessão agendada notifica o tutor do pet.</summary>
        public Task NotificarSessaoAgendada(Guid tutorUsuarioId, string nomePaciente, DateTime dataHora)
        {
            return Registrar(
                tutorUsuarioId,
                TiposDeNotificacao.SessaoAgendada,
                "Nova sessão agendada",
                $"Uma sessão de fisioterapia para {nomePaciente} foi marcada para {FormatarDataHora(dataHora)}. Confirme a presença.",
                LinkAgendaDoTutor);
        }

        /// <summary>HU-015, CA-2: lembrete quando a sessão se aproxima sem confirmação.</summary>
        public Task NotificarLembreteConfirmacao(Guid tutorUsuarioId, string nomePaciente, DateTime dataHora)
        {
            return Registrar(
                tutorUsuarioId,
                TiposDeNotificacao.LembreteConfirmacao,
                "Confirme a presença",
                $"A sessão de {nomePaciente} em {FormatarDataHora(dataHora)} ainda aguarda confirmação.",
                LinkAgendaDoTutor);
        }

        /// <summary>
        /// HU-006: avisa a outra parte quando a presença é confirmada, cancelada ou a sessão
        /// concluída. O tutor é levado à própria agenda; a equipe, à agenda do veterinário.
        /// </summary>
        public Task NotificarMudancaStatusSessao(
            Guid destinatarioUsuarioId,
            string nomePaciente,
            string novoStatus,
            DateTime dataHora,
            bool destinatarioEhTutor)
        {
            var statusEmMinusculas = novoStatus.ToLowerInvariant();

            return Registrar(
                destinatarioUsuarioId,
                TiposDeNotificacao.StatusSessao,
                $"Sessão {statusEmMinusculas}",
                $"A sessão de {nomePaciente} em {FormatarDataHora(dataHora)} foi marcada como {statusEmMinusculas}.",
                destinatarioEhTutor ? LinkAgendaDoTutor : LinkAgendaDoVeterinario);
        }

        /// <summary>HU-015, CA-3: novo registro adicionado ao prontuário.</summary>
        public Task NotificarNovoRegistroProntuario(
            Guid tutorUsuarioId,
            string nomePaciente,
            string tipoRegistro,
            Guid pacienteId)
        {
            return Registrar(
                tutorUsuarioId,
                TiposDeNotificacao.NovoRegistroProntuario,
                "Prontuário atualizado",
                $"{tipoRegistro} registrado no prontuário de {nomePaciente}.",
                LinkProntuario(pacienteId));
        }

        /// <summary>HU-015, CA-3: nova mensagem recebida.</summary>
        public Task NotificarNovaMensagem(Guid destinatarioId, string nomeRemetente, string trecho)
        {
            var resumo = trecho.Length > 80 ? trecho[..80] + "..." : trecho;

            return Registrar(
                destinatarioId,
                TiposDeNotificacao.NovaMensagem,
                $"Nova mensagem de {nomeRemetente}",
                resumo,
                LinkMensagens);
        }

        /// <summary>Aviso de dose de vacina ou vermífugo se aproximando do vencimento.</summary>
        public Task NotificarDoseDeVacina(
            Guid tutorUsuarioId,
            string nomePaciente,
            string nomeProduto,
            DateTime proximaDose,
            Guid pacienteId)
        {
            var dias = (proximaDose.Date - DateTime.UtcNow.Date).Days;

            var prazo = dias switch
            {
                < 0 => "está vencida",
                0 => "vence hoje",
                1 => "vence amanhã",
                _ => $"vence em {dias} dias"
            };

            return Registrar(
                tutorUsuarioId,
                TiposDeNotificacao.DoseDeVacina,
                "Prevenção a vencer",
                $"A dose de {nomeProduto} de {nomePaciente} {prazo}. Agende o reforço com a clínica.",
                LinkProntuario(pacienteId));
        }

        /// <summary>O veterinário que passa a acompanhar o paciente é avisado na hora.</summary>
        public Task NotificarPacienteRecebido(Guid veterinarioUsuarioId, string nomePaciente, Guid pacienteId)
        {
            return Registrar(
                veterinarioUsuarioId,
                TiposDeNotificacao.PacienteTransferido,
                "Novo paciente sob sua responsabilidade",
                $"{nomePaciente} foi transferido para você. Os tratamentos em andamento e as sessões futuras já estão na sua agenda.",
                LinkProntuario(pacienteId));
        }

        /// <summary>Quem deixa de acompanhar o paciente fica sabendo para onde ele foi.</summary>
        public Task NotificarPacienteTransferido(Guid veterinarioUsuarioId, string nomePaciente, string nomeNovoVeterinario)
        {
            return Registrar(
                veterinarioUsuarioId,
                TiposDeNotificacao.PacienteTransferido,
                "Paciente transferido",
                $"{nomePaciente} passou a ser acompanhado por {nomeNovoVeterinario} e saiu da sua lista de pacientes.",
                null);
        }

        /// <summary>O tutor sabe quem é o profissional que cuida do seu pet.</summary>
        public Task NotificarNovoVeterinarioResponsavel(Guid tutorUsuarioId, string nomePaciente, string nomeVeterinario, Guid pacienteId)
        {
            return Registrar(
                tutorUsuarioId,
                TiposDeNotificacao.PacienteTransferido,
                "Novo veterinário responsável",
                $"{nomeVeterinario} passou a ser o veterinário responsável por {nomePaciente}.",
                LinkProntuario(pacienteId));
        }

        private async Task Registrar(Guid usuarioId, string tipo, string titulo, string conteudo, string? link)
        {
            if (usuarioId == Guid.Empty)
            {
                return;
            }

            await _repositorio.Adicionar(new Notificacao
            {
                UsuarioId = usuarioId,
                Tipo = tipo,
                Titulo = titulo,
                Conteudo = conteudo,
                LinkRelacionado = link
            });

            await _repositorio.SalvarAlteracoes();

            // Gravou: o entregador pode mandar o e-mail e o push agora, sem esperar a varredura.
            _sinal.Acordar();
        }

        private static string FormatarDataHora(DateTime dataHora)
        {
            var local = dataHora.Kind == DateTimeKind.Utc ? dataHora.ToLocalTime() : dataHora;
            return local.ToString("dd/MM/yyyy 'às' HH:mm");
        }
    }
}
