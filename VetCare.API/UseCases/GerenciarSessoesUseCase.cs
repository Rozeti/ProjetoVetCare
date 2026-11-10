using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Gestão das sessões cadastradas: a lista da clínica por situação (agendadas, aguardando
    /// registro, realizadas, canceladas e inativadas) e a inativação e reativação de sessões.
    /// </summary>
    public class GerenciarSessoesUseCase
    {
        /// <summary>Tamanho da coluna Observacoes da sessão.</summary>
        private const int TamanhoDasObservacoes = 1000;

        private readonly ISessaoRepository _sessoes;
        private readonly IAtendimentoRepository _atendimentos;
        private readonly IPetRepository _pets;
        private readonly ITratamentoRepository _tratamentos;
        private readonly IClinicaRepository _clinicas;
        private readonly IBloqueioAgendaRepository _bloqueios;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarSessoesUseCase(
            ISessaoRepository sessoes,
            IAtendimentoRepository atendimentos,
            IPetRepository pets,
            ITratamentoRepository tratamentos,
            IClinicaRepository clinicas,
            IBloqueioAgendaRepository bloqueios,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _sessoes = sessoes;
            _atendimentos = atendimentos;
            _pets = pets;
            _tratamentos = tratamentos;
            _clinicas = clinicas;
            _bloqueios = bloqueios;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>
        /// Tela de gestão: filtra por animal, tratamento, veterinário, período (dias da clínica) e
        /// situação, e conta as sessões de cada situação com os mesmos filtros.
        /// </summary>
        public async Task<Resultado<GestaoDeSessoesDTO>> Listar(
            Guid? pacienteId,
            Guid? tratamentoId,
            Guid? veterinarioId,
            DateTime? inicio,
            DateTime? fim,
            string? situacao,
            ParametrosPagina parametros)
        {
            var relogio = RelogioDaClinica.Padrao;

            if (inicio.HasValue && fim.HasValue && relogio.DiaDaClinica(fim.Value) < relogio.DiaDaClinica(inicio.Value))
            {
                return Resultado<GestaoDeSessoesDTO>.Invalido("A data final não pode ser anterior à inicial.");
            }

            var situacaoNormalizada = SituacaoSessao.Normalizar(situacao);

            if (!string.IsNullOrWhiteSpace(situacao) && situacaoNormalizada == null)
            {
                return Resultado<GestaoDeSessoesDTO>.Invalido(
                    $"Situação inválida. Use uma destas: {string.Join(", ", SituacaoSessao.Todas)}.");
            }

            if (pacienteId.HasValue)
            {
                var pet = await _pets.ObterPorId(pacienteId.Value);

                if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<GestaoDeSessoesDTO>.NaoEncontrado("Paciente não encontrado.");
                }

                if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
                {
                    return Resultado<GestaoDeSessoesDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
                }
            }

            if (tratamentoId.HasValue)
            {
                var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(tratamentoId.Value);

                if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<GestaoDeSessoesDTO>.NaoEncontrado("Tratamento não encontrado.");
                }
            }

            var filtro = new FiltroDeSessoes
            {
                PacienteId = pacienteId,
                TratamentoId = tratamentoId,
                VeterinarioId = veterinarioId,
                InicioUtc = inicio.HasValue ? relogio.ParaUtc(relogio.DiaDaClinica(inicio.Value)) : null,
                FimUtc = fim.HasValue ? relogio.ParaUtc(relogio.DiaDaClinica(fim.Value).AddDays(1)) : null
            };

            var agora = DateTime.UtcNow;
            var pagina = await _sessoes.Listar(_usuarioAtual.ClinicaId, filtro, situacaoNormalizada, agora, parametros);
            var registros = await _atendimentos.ObterPorSessoes(pagina.Itens.Select(s => s.Id));
            var horasMinimas = (await _clinicas.ObterPorId(_usuarioAtual.ClinicaId))?.HorasMinimasCancelamento ?? 12;

            return Resultado<GestaoDeSessoesDTO>.Ok(new GestaoDeSessoesDTO
            {
                Pagina = pagina.Converter(s => CadastrarSessoesUseCase.MapearParaDTO(s, horasMinimas, registros.GetValueOrDefault(s.Id))),
                Contagens = await _sessoes.ContarPorSituacao(_usuarioAtual.ClinicaId, filtro, agora)
            });
        }

        /// <summary>
        /// Retira do plano uma sessão cadastrada por engano ou que deixou de ser necessária. Ela sai
        /// da agenda e das contagens, mas fica no histórico. A sessão com atendimento registrado faz
        /// parte do prontuário (RN-004) e não é inativada.
        /// </summary>
        public async Task<Resultado> Inativar(Guid sessaoId, InativarSessaoDTO dto)
        {
            var (sessao, falha) = await ObterParaAlterar(sessaoId);

            if (sessao == null)
            {
                return falha!;
            }

            if (sessao.Inativa)
            {
                return Resultado.Conflito("Esta sessão já está inativada.");
            }

            if (await _atendimentos.ObterPorSessao(sessao.Id) != null)
            {
                return Resultado.Conflito(
                    "Esta sessão tem atendimento registrado e faz parte do prontuário, por isso não pode ser inativada. " +
                    "Se algo estiver errado, corrija o registro do atendimento.");
            }

            if (sessao.Status == StatusSessao.Concluida)
            {
                return Resultado.Conflito("Uma sessão concluída não pode ser inativada.");
            }

            var aindaIaAcontecer = sessao.DataHora > DateTime.UtcNow && sessao.Status != StatusSessao.Cancelada;
            var motivo = dto.Motivo?.Trim() ?? string.Empty;

            sessao.Inativa = true;
            Anotar(sessao, string.IsNullOrEmpty(motivo) ? "Inativada" : $"Inativada: {motivo}");

            _sessoes.Atualizar(sessao);
            await _sessoes.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Inativacao, "Sessao", sessao.Id,
                $"Sessão de {sessao.Tratamento?.Paciente?.Nome} em {RelogioDaClinica.Padrao.Formatar(sessao.DataHora)} " +
                $"inativada no tratamento \"{sessao.Tratamento?.Nome}\"" +
                (string.IsNullOrEmpty(motivo) ? string.Empty : $". Motivo: {motivo}"));

            // O tutor contava com a sessão: ele é avisado de que ela saiu da agenda.
            var usuarioTutor = sessao.Tratamento?.Paciente?.Tutor?.UsuarioId;

            if (aindaIaAcontecer && usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarSessaoRetirada(usuarioTutor.Value, sessao.Tratamento!.Paciente!.Nome, sessao.DataHora);
            }

            return Resultado.Ok("Sessão inativada. Ela saiu da agenda e pode ser reativada.");
        }

        /// <summary>Devolve a sessão inativada ao plano, se o horário ainda estiver livre (RN-002).</summary>
        public async Task<Resultado> Reativar(Guid sessaoId)
        {
            var (sessao, falha) = await ObterParaAlterar(sessaoId);

            if (sessao == null)
            {
                return falha!;
            }

            if (!sessao.Inativa)
            {
                return Resultado.Conflito("Esta sessão não está inativada.");
            }

            var tratamento = sessao.Tratamento!;

            if (!StatusTratamento.Ativo(tratamento.Status))
            {
                return Resultado.Conflito(
                    $"O tratamento \"{tratamento.Nome}\" está {tratamento.Status.ToLowerInvariant()}; a sessão não pode voltar para o plano.");
            }

            if (tratamento.Paciente?.EmAcompanhamento != true)
            {
                return Resultado.Conflito("O paciente está inativo ou tem óbito registrado; a sessão não pode ser reativada.");
            }

            var volta = sessao.Status != StatusSessao.Cancelada;

            if (volta && await _sessoes.ExisteConflitoHorario(sessao.VeterinarioId, sessao.DataHora, sessao.DataHoraFim, sessao.Id))
            {
                return Resultado.Conflito(
                    "O horário desta sessão já está ocupado por outra sessão do veterinário. Cadastre uma nova sessão em outro horário.");
            }

            var futura = sessao.DataHora > DateTime.UtcNow;

            if (volta && futura)
            {
                var bloqueio = await _bloqueios.ObterConflito(sessao.VeterinarioId, sessao.DataHora, sessao.DataHoraFim);

                if (bloqueio != null)
                {
                    return Resultado.Conflito($"A agenda do veterinário está bloqueada neste período ({bloqueio.Motivo}).");
                }
            }

            sessao.Inativa = false;
            Anotar(sessao, "Reativada");

            _sessoes.Atualizar(sessao);
            await _sessoes.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Sessao", sessao.Id,
                $"Sessão de {tratamento.Paciente?.Nome} em {RelogioDaClinica.Padrao.Formatar(sessao.DataHora)} " +
                $"reativada no tratamento \"{tratamento.Nome}\"");

            var usuarioTutor = tratamento.Paciente?.Tutor?.UsuarioId;

            if (volta && futura && usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarSessaoAgendada(usuarioTutor.Value, tratamento.Paciente!.Nome, sessao.DataHora);
            }

            return Resultado.Ok("Sessão reativada.");
        }

        /// <summary>A sessão da clínica, desde que quem pede possa alterá-la.</summary>
        private async Task<(Sessao? Sessao, Resultado? Falha)> ObterParaAlterar(Guid sessaoId)
        {
            var sessao = await _sessoes.ObterPorIdComRelacionamentos(sessaoId);

            if (sessao == null || sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return (null, Resultado.NaoEncontrado("Sessão não encontrada."));
            }

            if (!_usuarioAtual.EhAdministrador && !_usuarioAtual.EhVeterinario && !_usuarioAtual.EhApoio)
            {
                return (null, Resultado.NaoAutorizado("Somente a equipe da clínica pode inativar ou reativar sessões."));
            }

            // Como na confirmação e no cancelamento, o veterinário mexe só na própria agenda.
            if (_usuarioAtual.EhVeterinario && sessao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return (null, Resultado.NaoAutorizado("Você só pode inativar ou reativar sessões da sua própria agenda."));
            }

            return (sessao, null);
        }

        private static void Anotar(Sessao sessao, string anotacao)
        {
            var texto = string.IsNullOrWhiteSpace(sessao.Observacoes) ? anotacao : $"{sessao.Observacoes} | {anotacao}";

            // As observações têm tamanho fixo no banco; a anotação não pode derrubar a operação.
            sessao.Observacoes = texto.Length <= TamanhoDasObservacoes ? texto : texto[..TamanhoDasObservacoes];
        }
    }
}
