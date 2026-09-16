using VetCare.API.Data;
using VetCare.API.Models;

namespace VetCare.API.Services
{
    /// <summary>
    /// Centraliza os quatro gatilhos de notificação previstos na HU-015, para que os
    /// casos de uso apenas informem o evento ocorrido.
    /// </summary>
    public class NotificacaoService
    {
        private readonly INotificacaoRepository _repositorio;

        public NotificacaoService(INotificacaoRepository repositorio)
        {
            _repositorio = repositorio;
        }

        public static class Tipos
        {
            public const string SessaoAgendada = "SessaoAgendada";
            public const string LembreteConfirmacao = "LembreteConfirmacao";
            public const string StatusSessao = "StatusSessao";
            public const string NovoRegistroProntuario = "NovoRegistroProntuario";
            public const string NovaMensagem = "NovaMensagem";
            public const string DoseDeVacina = "DoseDeVacina";
        }

        /// <summary>HU-015, CA-1: uma nova sessão agendada notifica o tutor do pet.</summary>
        public Task NotificarSessaoAgendada(Guid tutorUsuarioId, string nomePaciente, DateTime dataHora)
        {
            return Registrar(
                tutorUsuarioId,
                Tipos.SessaoAgendada,
                "Nova sessão agendada",
                $"Uma sessão de fisioterapia para {nomePaciente} foi marcada para {FormatarDataHora(dataHora)}. Confirme a presença.",
                "/agenda");
        }

        /// <summary>HU-015, CA-2: lembrete quando a sessão se aproxima sem confirmação.</summary>
        public Task NotificarLembreteConfirmacao(Guid tutorUsuarioId, string nomePaciente, DateTime dataHora)
        {
            return Registrar(
                tutorUsuarioId,
                Tipos.LembreteConfirmacao,
                "Confirme a presença",
                $"A sessão de {nomePaciente} em {FormatarDataHora(dataHora)} ainda aguarda confirmação.",
                "/agenda");
        }

        /// <summary>Informa o veterinário quando o tutor confirma ou cancela a presença (HU-006).</summary>
        public Task NotificarMudancaStatusSessao(
            Guid veterinarioUsuarioId,
            string nomePaciente,
            string novoStatus,
            DateTime dataHora)
        {
            return Registrar(
                veterinarioUsuarioId,
                Tipos.StatusSessao,
                $"Sessão {novoStatus.ToLowerInvariant()}",
                $"A sessão de {nomePaciente} em {FormatarDataHora(dataHora)} foi marcada como {novoStatus.ToLowerInvariant()}.",
                "/agenda");
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
                Tipos.NovoRegistroProntuario,
                "Prontuário atualizado",
                $"{tipoRegistro} registrado no prontuário de {nomePaciente}.",
                $"/prontuario/{pacienteId}");
        }

        /// <summary>HU-015, CA-3: nova mensagem recebida.</summary>
        public Task NotificarNovaMensagem(Guid destinatarioId, string nomeRemetente, string trecho)
        {
            var resumo = trecho.Length > 80 ? trecho[..80] + "..." : trecho;

            return Registrar(
                destinatarioId,
                Tipos.NovaMensagem,
                $"Nova mensagem de {nomeRemetente}",
                resumo,
                "/mensagens");
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
                Tipos.DoseDeVacina,
                "Prevenção a vencer",
                $"A dose de {nomeProduto} de {nomePaciente} {prazo}. Agende o reforço com a clínica.",
                $"/prontuario/{pacienteId}");
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
        }

        private static string FormatarDataHora(DateTime dataHora)
        {
            var local = dataHora.Kind == DateTimeKind.Utc ? dataHora.ToLocalTime() : dataHora;
            return local.ToString("dd/MM/yyyy 'às' HH:mm");
        }
    }
}
