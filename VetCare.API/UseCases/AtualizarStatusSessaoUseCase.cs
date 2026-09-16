using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-006: confirmação e cancelamento de presença. O tutor age sobre as sessões dos
    /// próprios pets e respeita a antecedência mínima da clínica (RN-009); a equipe da
    /// clínica também pode concluir a sessão.
    /// </summary>
    public class AtualizarStatusSessaoUseCase
    {
        private static readonly string[] StatusPermitidos = { "Confirmada", "Cancelada", "Concluída" };

        private readonly ISessaoRepository _sessoes;
        private readonly IClinicaRepository _clinicas;
        private readonly NotificacaoService _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public AtualizarStatusSessaoUseCase(
            ISessaoRepository sessoes,
            IClinicaRepository clinicas,
            NotificacaoService notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _sessoes = sessoes;
            _clinicas = clinicas;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado> Executar(Guid sessaoId, AtualizarStatusSessaoDTO dto)
        {
            var novoStatus = StatusPermitidos.FirstOrDefault(
                s => string.Equals(s, dto.Status, StringComparison.OrdinalIgnoreCase));

            if (novoStatus == null)
            {
                return Resultado.Invalido(
                    $"Status inválido. Use um destes: {string.Join(", ", StatusPermitidos)}.");
            }

            var sessao = await _sessoes.ObterPorIdComRelacionamentos(sessaoId);

            if (sessao == null)
            {
                return Resultado.NaoEncontrado("Sessão não encontrada.");
            }

            if (sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoAutorizado("Esta sessão pertence a outra clínica.");
            }

            if (sessao.Status == "Concluída")
            {
                return Resultado.Invalido("Esta sessão já foi concluída e não pode mais ser alterada.");
            }

            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);
            var horasMinimas = clinica?.HorasMinimasCancelamento ?? 12;

            if (_usuarioAtual.EhTutor)
            {
                var validacao = ValidarAcaoDoTutor(sessao.Tratamento?.Paciente?.TutorId, novoStatus, sessao.DataHora, horasMinimas);

                if (!validacao.Sucesso)
                {
                    return validacao;
                }
            }
            else if (_usuarioAtual.EhVeterinario && sessao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado.NaoAutorizado("Você só pode alterar sessões da sua própria agenda.");
            }

            sessao.Status = novoStatus;

            if (!string.IsNullOrWhiteSpace(dto.Motivo))
            {
                var prefixo = string.IsNullOrWhiteSpace(sessao.Observacoes) ? string.Empty : sessao.Observacoes + " | ";
                sessao.Observacoes = $"{prefixo}{novoStatus}: {dto.Motivo.Trim()}";
            }

            _sessoes.Atualizar(sessao);
            await _sessoes.SalvarAlteracoes();

            await NotificarInteressados(sessao.Id, novoStatus);

            return Resultado.Ok($"Status da sessão atualizado para '{novoStatus}' com sucesso.");
        }

        private Resultado ValidarAcaoDoTutor(Guid? tutorDaSessao, string novoStatus, DateTime dataHora, int horasMinimas)
        {
            if (tutorDaSessao != _usuarioAtual.TutorId)
            {
                return Resultado.NaoAutorizado("Esta sessão não pertence a um pet sob sua responsabilidade.");
            }

            if (novoStatus == "Concluída")
            {
                return Resultado.NaoAutorizado("Somente a equipe da clínica pode concluir uma sessão.");
            }

            // HU-006, CA-3 e RN-009: cancelamento fora do prazo mínimo é sinalizado.
            if (novoStatus == "Cancelada")
            {
                var antecedencia = dataHora - DateTime.UtcNow;

                if (antecedencia < TimeSpan.FromHours(horasMinimas))
                {
                    return Resultado.Invalido(
                        $"O cancelamento deve ser feito com pelo menos {horasMinimas} horas de antecedência. " +
                        "Entre em contato com a clínica.");
                }
            }

            return Resultado.Ok();
        }

        private async Task NotificarInteressados(Guid sessaoId, string novoStatus)
        {
            var sessao = await _sessoes.ObterPorIdComRelacionamentos(sessaoId);

            if (sessao == null)
            {
                return;
            }

            var nomePaciente = sessao.Tratamento?.Paciente?.Nome ?? "paciente";

            // Quando o tutor age, o veterinário é avisado (HU-006, CA-1 e CA-2).
            if (_usuarioAtual.EhTutor)
            {
                var usuarioVeterinario = sessao.Veterinario?.UsuarioId;

                if (usuarioVeterinario.HasValue)
                {
                    await _notificacoes.NotificarMudancaStatusSessao(
                        usuarioVeterinario.Value, nomePaciente, novoStatus, sessao.DataHora);
                }

                return;
            }

            // Quando a clínica age, quem é avisado é o tutor.
            var usuarioTutor = sessao.Tratamento?.Paciente?.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarMudancaStatusSessao(
                    usuarioTutor.Value, nomePaciente, novoStatus, sessao.DataHora);
            }
        }
    }
}
