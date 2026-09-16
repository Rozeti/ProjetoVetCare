using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-016: indicadores operacionais do dia. A RN-008 define o escopo — o veterinário
    /// vê apenas os próprios pacientes e sessões; administrador e apoio veem a clínica toda.
    /// </summary>
    public class ConsultarIndicadoresUseCase
    {
        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly IAtendimentoRepository _atendimentos;
        private readonly ISessaoRepository _sessoes;
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IMensagemRepository _mensagens;
        private readonly INotificacaoRepository _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public ConsultarIndicadoresUseCase(
            IAvaliacaoRepository avaliacoes,
            IAtendimentoRepository atendimentos,
            ISessaoRepository sessoes,
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            IMensagemRepository mensagens,
            INotificacaoRepository notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _avaliacoes = avaliacoes;
            _atendimentos = atendimentos;
            _sessoes = sessoes;
            _pets = pets;
            _veterinarios = veterinarios;
            _mensagens = mensagens;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<IndicadoresDTO>> Executar(DateTime? data)
        {
            var referencia = data ?? DateTime.Now;
            var (inicio, fim) = ConsultarAgendaUseCase.CalcularIntervalo(referencia, "dia");

            // RN-008: veterinário enxerga somente o próprio recorte.
            var filtroVeterinario = _usuarioAtual.EhVeterinario ? _usuarioAtual.VeterinarioId : null;

            var indicadores = new IndicadoresDTO
            {
                Data = referencia.Date,
                Escopo = filtroVeterinario.HasValue ? "Veterinario" : "Clinica",
                AvaliacoesDoDia = await _avaliacoes.ContarPorPeriodo(_usuarioAtual.ClinicaId, filtroVeterinario, inicio, fim),
                AtendimentosDoDia = await _atendimentos.ContarPorPeriodo(_usuarioAtual.ClinicaId, filtroVeterinario, inicio, fim),
                PacientesAtivos = await _pets.ContarAtivos(_usuarioAtual.ClinicaId),
                MensagensNaoLidas = await _mensagens.ContarNaoLidas(_usuarioAtual.Id),
                NotificacoesNaoVisualizadas = await _notificacoes.ContarNaoVisualizadas(_usuarioAtual.Id)
            };

            var sessoesDoDia = await ObterSessoesDoDia(filtroVeterinario, inicio, fim);

            indicadores.SessoesDoDia = sessoesDoDia.Count;

            // HU-016, CA-1: confirmações pendentes é um dos quatro indicadores exigidos.
            indicadores.ConfirmacoesPendentes = sessoesDoDia.Count(s => s.Status == "Aguardando confirmação");

            indicadores.ProximasSessoes = sessoesDoDia
                .Where(s => s.Status != "Cancelada")
                .OrderBy(s => s.DataHora)
                .Take(5)
                .Select(s => new ItemAgendaDTO
                {
                    SessaoId = s.Id,
                    PacienteId = s.Tratamento?.PacienteId ?? Guid.Empty,
                    TratamentoId = s.TratamentoId,
                    VeterinarioId = s.VeterinarioId,
                    DataHora = s.DataHora,
                    NomePaciente = s.Tratamento?.Paciente?.Nome ?? string.Empty,
                    NomeTutor = s.Tratamento?.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                    NomeVeterinario = s.Veterinario?.Usuario?.Nome ?? string.Empty,
                    Status = s.Status
                })
                .ToList();

            // HU-016, CA-3: dia sem registros devolve os indicadores zerados, sem erro.
            return Resultado<IndicadoresDTO>.Ok(indicadores);
        }

        private async Task<List<Models.Sessao>> ObterSessoesDoDia(Guid? filtroVeterinario, DateTime inicio, DateTime fim)
        {
            if (filtroVeterinario.HasValue)
            {
                return await _sessoes.ObterPorPeriodo(filtroVeterinario.Value, inicio, fim);
            }

            return await _sessoes.ObterAgendaGeral(_usuarioAtual.ClinicaId, inicio, fim, null);
        }
    }
}
