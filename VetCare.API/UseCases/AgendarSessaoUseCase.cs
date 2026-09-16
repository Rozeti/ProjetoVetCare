using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-004: reserva de horário na agenda do veterinário.</summary>
    public class AgendarSessaoUseCase
    {
        private readonly ISessaoRepository _sessoes;
        private readonly ITratamentoRepository _tratamentos;
        private readonly IClinicaRepository _clinicas;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IBloqueioAgendaRepository _bloqueios;
        private readonly NotificacaoService _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public AgendarSessaoUseCase(
            ISessaoRepository sessoes,
            ITratamentoRepository tratamentos,
            IClinicaRepository clinicas,
            IVeterinarioRepository veterinarios,
            IBloqueioAgendaRepository bloqueios,
            NotificacaoService notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _sessoes = sessoes;
            _tratamentos = tratamentos;
            _clinicas = clinicas;
            _veterinarios = veterinarios;
            _bloqueios = bloqueios;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<SessaoDTO>> Executar(AgendarSessaoDTO dto)
        {
            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<SessaoDTO>.Invalido("Informe o veterinário responsável pela sessão.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<SessaoDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            // O veterinário administra a própria agenda; administração e apoio agendam para qualquer um.
            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<SessaoDTO>.NaoAutorizado("Você só pode agendar sessões na sua própria agenda.");
            }

            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(dto.TratamentoId);

            if (tratamento == null)
            {
                return Resultado<SessaoDTO>.NaoEncontrado("Tratamento não encontrado.");
            }

            if (tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<SessaoDTO>.NaoAutorizado("Este tratamento pertence a outra clínica.");
            }

            if (tratamento.Status != "Em Andamento")
            {
                return Resultado<SessaoDTO>.Invalido(
                    "Não é possível agendar sessões para um tratamento que não está em andamento.");
            }

            var dataHora = NormalizarParaUtc(dto.DataHora);

            // HU-004, CA-5: agendamento retroativo é bloqueado.
            if (dataHora <= DateTime.UtcNow)
            {
                return Resultado<SessaoDTO>.Invalido("Não é possível agendar sessões em datas ou horários passados.");
            }

            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);
            var duracao = clinica?.DuracaoSessaoMinutos ?? 60;

            // HU-004, CA-1: o horário precisa estar dentro do funcionamento da clínica.
            if (clinica != null && !DentroDoExpediente(dataHora, duracao, clinica))
            {
                return Resultado<SessaoDTO>.Invalido(
                    $"A clínica atende das {clinica.HorarioAbertura:hh\\:mm} às {clinica.HorarioFechamento:hh\\:mm}. " +
                    "Escolha um horário dentro desse período.");
            }

            // RN-002: um veterinário não pode ter duas sessões sobrepostas.
            if (await _sessoes.ExisteConflitoHorario(veterinarioId, dataHora, duracao))
            {
                return Resultado<SessaoDTO>.Conflito(
                    "Horário indisponível. O veterinário já possui uma sessão neste intervalo.");
            }

            // Além de não haver outra sessão, o profissional precisa estar disponível.
            var bloqueio = await _bloqueios.ObterConflito(veterinarioId, dataHora, dataHora.AddMinutes(duracao));

            if (bloqueio != null)
            {
                return Resultado<SessaoDTO>.Conflito(
                    $"A agenda do veterinário está bloqueada neste período ({bloqueio.Motivo}).");
            }

            var sessao = new Sessao
            {
                TratamentoId = dto.TratamentoId,
                VeterinarioId = veterinarioId,
                DataHora = dataHora,
                Status = "Aguardando confirmação",
                Observacoes = dto.Observacoes.Trim()
            };

            await _sessoes.Adicionar(sessao);
            await _sessoes.SalvarAlteracoes();

            // HU-015, CA-1: o tutor é avisado da nova sessão.
            var usuarioTutor = tratamento.Paciente?.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarSessaoAgendada(
                    usuarioTutor.Value,
                    tratamento.Paciente?.Nome ?? "seu pet",
                    dataHora);
            }

            sessao.Tratamento = tratamento;
            sessao.Veterinario = veterinario;

            var horasCancelamento = clinica?.HorasMinimasCancelamento ?? 12;

            return Resultado<SessaoDTO>.Ok(
                MapearParaDTO(sessao, horasCancelamento, false),
                "Sessão agendada com sucesso.");
        }

        private static bool DentroDoExpediente(DateTime dataHoraUtc, int duracaoMinutos, Clinica clinica)
        {
            var local = dataHoraUtc.ToLocalTime();
            var inicio = local.TimeOfDay;
            var fim = local.AddMinutes(duracaoMinutos).TimeOfDay;

            return inicio >= clinica.HorarioAbertura && fim <= clinica.HorarioFechamento;
        }

        public static DateTime NormalizarParaUtc(DateTime valor)
        {
            return valor.Kind switch
            {
                DateTimeKind.Utc => valor,
                DateTimeKind.Local => valor.ToUniversalTime(),
                _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
            };
        }

        public static SessaoDTO MapearParaDTO(Sessao sessao, int horasMinimasCancelamento, bool possuiAtendimento)
        {
            return new SessaoDTO
            {
                Id = sessao.Id,
                TratamentoId = sessao.TratamentoId,
                VeterinarioId = sessao.VeterinarioId,
                NomeVeterinario = sessao.Veterinario?.Usuario?.Nome ?? string.Empty,
                PacienteId = sessao.Tratamento?.PacienteId ?? Guid.Empty,
                NomePaciente = sessao.Tratamento?.Paciente?.Nome ?? string.Empty,
                NomeTutor = sessao.Tratamento?.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                DataHora = sessao.DataHora,
                Status = sessao.Status,
                Observacoes = sessao.Observacoes,
                PossuiAtendimento = possuiAtendimento,
                PodeCancelar = PodeCancelar(sessao, horasMinimasCancelamento)
            };
        }

        /// <summary>RN-009: o cancelamento pelo tutor respeita a antecedência mínima da clínica.</summary>
        public static bool PodeCancelar(Sessao sessao, int horasMinimasCancelamento)
        {
            if (sessao.Status is "Cancelada" or "Concluída")
            {
                return false;
            }

            return sessao.DataHora - DateTime.UtcNow >= TimeSpan.FromHours(horasMinimasCancelamento);
        }
    }
}
