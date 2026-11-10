using System.Globalization;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-004: cadastro das sessões de um tratamento. O plano é montado pela quantidade de sessões,
    /// pelos dias da semana e pelo horário (<see cref="Previa"/>); o cadastro grava as datas da
    /// prévia de uma vez (<see cref="Executar"/>). É o único caminho para criar sessões, e vale
    /// tanto para o plano inicial quanto para acrescentar sessões no meio ou no fim do tratamento.
    /// O cadastro é tudo ou nada: se uma sessão não couber na agenda, nenhuma é gravada.
    /// </summary>
    public class CadastrarSessoesUseCase
    {
        /// <summary>Menor e maior duração aceitas para uma sessão.</summary>
        public const int DuracaoMinimaMinutos = 15;
        public const int DuracaoMaximaMinutos = 240;

        /// <summary>Mesmo teto do cadastro do tratamento.</summary>
        public const int MaximoDeSessoesPrevistas = 365;

        private readonly ISessaoRepository _sessoes;
        private readonly ITratamentoRepository _tratamentos;
        private readonly IClinicaRepository _clinicas;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IBloqueioAgendaRepository _bloqueios;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public CadastrarSessoesUseCase(
            ISessaoRepository sessoes,
            ITratamentoRepository tratamentos,
            IClinicaRepository clinicas,
            IVeterinarioRepository veterinarios,
            IBloqueioAgendaRepository bloqueios,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _sessoes = sessoes;
            _tratamentos = tratamentos;
            _clinicas = clinicas;
            _veterinarios = veterinarios;
            _bloqueios = bloqueios;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>Tratamento, veterinário e clínica de um cadastro, já conferidos.</summary>
        private sealed record Contexto(Tratamento Tratamento, Veterinario Veterinario, Clinica? Clinica);

        /// <summary>
        /// Monta as datas de um plano de sessões: a partir do dia informado, nos dias da semana
        /// escolhidos, no mesmo horário. Um dia em que a sessão não cabe (horário já passado, outra
        /// sessão do veterinário ou agenda bloqueada) é pulado, e a sessão vai para o próximo dia
        /// do plano — assim a quantidade pedida é mantida. Nada é gravado.
        /// </summary>
        public async Task<Resultado<PreviaDeSessoesDTO>> Previa(PlanoDeSessoesDTO dto)
        {
            if (dto.Quantidade < 1 || dto.Quantidade > CadastrarSessoesDTO.MaximoPorCadastro)
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido($"Informe de 1 a {CadastrarSessoesDTO.MaximoPorCadastro} sessões.");
            }

            var dias = (dto.DiasDaSemana ?? new List<int>()).Where(d => d is >= 0 and <= 6).ToHashSet();

            if (dias.Count == 0)
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido("Escolha ao menos um dia da semana.");
            }

            if (dto.APartirDe == null)
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido("Informe a partir de qual data as sessões começam.");
            }

            if (!LerHora(dto.HoraInicio, out var horaInicio))
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido("Informe o horário de início das sessões (HH:mm).");
            }

            if (!LerHora(dto.HoraFim, out var horaFim))
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido("Informe o horário de término das sessões (HH:mm).");
            }

            var (falha, carregado) = await CarregarContexto(dto.TratamentoId, dto.VeterinarioId);

            if (falha != null || carregado == null)
            {
                return Resultado<PreviaDeSessoesDTO>.Erro(falha!.Falha, falha.Mensagem);
            }

            var contexto = carregado;
            var relogio = RelogioDaClinica.Padrao;
            var primeiroDia = relogio.DiaDaClinica(dto.APartirDe.Value);

            if (primeiroDia < relogio.Hoje)
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido("A primeira data não pode estar no passado.");
            }

            // O formato do horário vale para todas as datas: confere uma vez, no primeiro dia.
            var problema = ValidarIntervalo(
                relogio.ParaUtc(primeiroDia + horaInicio), relogio.ParaUtc(primeiroDia + horaFim), contexto.Clinica, conferirPassado: false);

            if (problema != null)
            {
                return Resultado<PreviaDeSessoesDTO>.Invalido(problema);
            }

            // A agenda do período inteiro vem de uma vez; cada data é conferida em memória.
            var limite = primeiroDia.AddDays(PlanoDeSessoesDTO.DiasProcurados);
            var veterinarioId = contexto.Veterinario.Id;
            var ocupadas = (await _sessoes.ObterPorPeriodo(veterinarioId, relogio.ParaUtc(primeiroDia).AddDays(-1), relogio.ParaUtc(limite)))
                .Where(s => s.Status != StatusSessao.Cancelada)
                .ToList();
            var bloqueios = await _bloqueios.ObterPorPeriodo(veterinarioId, relogio.ParaUtc(primeiroDia), relogio.ParaUtc(limite));

            var previa = new PreviaDeSessoesDTO();
            var agora = DateTime.UtcNow;

            for (var dia = primeiroDia; previa.Sessoes.Count < dto.Quantidade && dia < limite; dia = dia.AddDays(1))
            {
                if (!dias.Contains((int)dia.DayOfWeek))
                {
                    continue;
                }

                var inicio = relogio.ParaUtc(dia + horaInicio);
                var fim = relogio.ParaUtc(dia + horaFim);
                string? motivo = null;

                if (inicio <= agora)
                {
                    motivo = "o horário de hoje já passou";
                }
                else if (ocupadas.Any(s => s.DataHora < fim && inicio < s.DataHoraFim))
                {
                    motivo = "o veterinário já tem uma sessão neste horário";
                }
                else if (bloqueios.FirstOrDefault(b => b.Inicio < fim && inicio < b.Fim) is { } bloqueio)
                {
                    motivo = $"agenda bloqueada ({bloqueio.Motivo})";
                }

                if (motivo != null)
                {
                    previa.Puladas.Add(new DataPuladaDTO { Data = dia, Motivo = motivo });
                    continue;
                }

                previa.Sessoes.Add(new HorarioDeSessaoDTO { Inicio = inicio, Fim = fim });
            }

            if (previa.Sessoes.Count < dto.Quantidade)
            {
                return Resultado<PreviaDeSessoesDTO>.Conflito(
                    $"Só há {previa.Sessoes.Count} datas livres nos próximos 12 meses com esses dias e esse horário. " +
                    "Escolha outros dias da semana, outro horário ou uma quantidade menor.");
            }

            var cadastradas = await ContarCadastradas(contexto.Tratamento.Id);

            previa.SessoesPrevistas = contexto.Tratamento.SessoesPrevistas;
            previa.SessoesCadastradas = cadastradas;
            previa.NovoTotalPrevisto = NovoTotalPrevisto(contexto.Tratamento.SessoesPrevistas, cadastradas + dto.Quantidade);

            return Resultado<PreviaDeSessoesDTO>.Ok(previa);
        }

        /// <summary>
        /// Grava as sessões informadas (normalmente as datas da prévia). Quando o tratamento passa
        /// a ter mais sessões do que o previsto, o total previsto acompanha: é o plano sendo ampliado.
        /// </summary>
        public async Task<Resultado<List<SessaoDTO>>> Executar(CadastrarSessoesDTO dto)
        {
            if (dto.Sessoes == null || dto.Sessoes.Count == 0)
            {
                return Resultado<List<SessaoDTO>>.Invalido("Informe ao menos uma sessão, com o início e o fim.");
            }

            if (dto.Sessoes.Count > CadastrarSessoesDTO.MaximoPorCadastro)
            {
                return Resultado<List<SessaoDTO>>.Invalido($"Cadastre até {CadastrarSessoesDTO.MaximoPorCadastro} sessões por vez.");
            }

            var (falha, contexto) = await CarregarContexto(dto.TratamentoId, dto.VeterinarioId);

            if (falha != null || contexto == null)
            {
                return Resultado<List<SessaoDTO>>.Erro(falha!.Falha, falha.Mensagem);
            }

            var (tratamento, veterinario, clinica) = contexto;
            var veterinarioId = veterinario.Id;

            // Primeiro o formato de cada horário, depois o lote entre si e, por fim, a agenda.
            var horarios = new List<(DateTime Inicio, DateTime Fim)>();

            foreach (var item in dto.Sessoes)
            {
                if (item.Inicio == null)
                {
                    return Resultado<List<SessaoDTO>>.Invalido("Informe a data e o horário de início de cada sessão.");
                }

                if (item.Fim == null)
                {
                    return Resultado<List<SessaoDTO>>.Invalido(NaSessao("Informe o horário de término de cada sessão.", item.Inicio, dto.Sessoes.Count));
                }

                var inicio = NormalizarParaUtc(item.Inicio.Value);
                var fim = NormalizarParaUtc(item.Fim.Value);
                var problema = ValidarIntervalo(inicio, fim, clinica, conferirPassado: true);

                if (problema != null)
                {
                    return Resultado<List<SessaoDTO>>.Invalido(NaSessao(problema, item.Inicio, dto.Sessoes.Count));
                }

                horarios.Add((inicio, fim));
            }

            horarios = horarios.OrderBy(h => h.Inicio).ToList();

            for (var i = 1; i < horarios.Count; i++)
            {
                if (horarios[i].Inicio < horarios[i - 1].Fim)
                {
                    return Resultado<List<SessaoDTO>>.Invalido(
                        $"As sessões de {Formatar(horarios[i - 1].Inicio)} e de {Formatar(horarios[i].Inicio)} se sobrepõem. " +
                        "Ajuste os horários antes de cadastrar.");
                }
            }

            foreach (var (inicio, fim) in horarios)
            {
                // RN-002: um veterinário não pode ter duas sessões sobrepostas.
                if (await _sessoes.ExisteConflitoHorario(veterinarioId, inicio, fim))
                {
                    return Resultado<List<SessaoDTO>>.Conflito(NaSessao(
                        "Horário indisponível. O veterinário já possui uma sessão neste intervalo.", inicio, horarios.Count));
                }

                // Além de não haver outra sessão, o profissional precisa estar disponível.
                var bloqueio = await _bloqueios.ObterConflito(veterinarioId, inicio, fim);

                if (bloqueio != null)
                {
                    return Resultado<List<SessaoDTO>>.Conflito(NaSessao(
                        $"A agenda do veterinário está bloqueada neste período ({bloqueio.Motivo}).", inicio, horarios.Count));
                }
            }

            var sessoes = horarios.Select(h => new Sessao
            {
                TratamentoId = tratamento.Id,
                VeterinarioId = veterinarioId,
                DataHora = h.Inicio,
                DataHoraFim = h.Fim,
                Status = StatusSessao.AguardandoConfirmacao,
                Observacoes = dto.Observacoes.Trim(),
                Tratamento = tratamento,
                Veterinario = veterinario
            }).ToList();

            // Sessões além do previsto ampliam o plano do tratamento, na mesma gravação.
            var previstasAntes = tratamento.SessoesPrevistas;
            var novoTotal = NovoTotalPrevisto(previstasAntes, await ContarCadastradas(tratamento.Id) + sessoes.Count);
            tratamento.SessoesPrevistas = novoTotal;

            foreach (var sessao in sessoes)
            {
                await _sessoes.Adicionar(sessao);
            }

            await _sessoes.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Sessao", sessoes[0].Id,
                $"{Contar(sessoes.Count)} do tratamento \"{tratamento.Nome}\" de {tratamento.Paciente!.Nome}: " +
                string.Join(", ", sessoes.Take(12).Select(s => Formatar(s.DataHora))) + (sessoes.Count > 12 ? "..." : string.Empty));

            if (novoTotal != previstasAntes)
            {
                await _auditoria.RegistrarDoUsuarioAtual(
                    AuditoriaService.Acoes.Alteracao, "Tratamento", tratamento.Id,
                    $"Plano do tratamento \"{tratamento.Nome}\" de {tratamento.Paciente.Nome} ampliado de {previstasAntes} " +
                    $"para {novoTotal} sessões.");
            }

            // HU-015, CA-1: o tutor é avisado das novas sessões num único aviso.
            var usuarioTutor = tratamento.Paciente.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                if (sessoes.Count == 1)
                {
                    await _notificacoes.NotificarSessaoAgendada(usuarioTutor.Value, tratamento.Paciente.Nome, sessoes[0].DataHora);
                }
                else
                {
                    await _notificacoes.NotificarSessoesAgendadas(
                        usuarioTutor.Value, tratamento.Paciente.Nome, sessoes.Count, sessoes[0].DataHora);
                }
            }

            var horasCancelamento = clinica?.HorasMinimasCancelamento ?? 12;
            var mensagem = sessoes.Count == 1 ? "Sessão cadastrada." : $"{sessoes.Count} sessões cadastradas.";

            if (novoTotal != previstasAntes)
            {
                mensagem += $" O tratamento passa a prever {novoTotal} sessões.";
            }

            return Resultado<List<SessaoDTO>>.Ok(sessoes.Select(s => MapearParaDTO(s, horasCancelamento, null)).ToList(), mensagem);
        }

        /// <summary>Confere o veterinário, o tratamento e o paciente de um cadastro de sessões.</summary>
        private async Task<(Resultado? Falha, Contexto? Contexto)> CarregarContexto(Guid tratamentoId, Guid? veterinarioInformado)
        {
            var veterinarioId = veterinarioInformado ?? _usuarioAtual.VeterinarioId ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return (Resultado.Invalido("Informe o veterinário responsável pelas sessões."), null);
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return (Resultado.NaoEncontrado("Veterinário não encontrado nesta clínica."), null);
            }

            // O veterinário administra a própria agenda; administração e apoio cadastram para qualquer um.
            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return (Resultado.NaoAutorizado("Você só pode cadastrar sessões na sua própria agenda."), null);
            }

            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(tratamentoId);

            if (tratamento == null)
            {
                return (Resultado.NaoEncontrado("Tratamento não encontrado."), null);
            }

            if (tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return (Resultado.NaoAutorizado("Este tratamento pertence a outra clínica."), null);
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, tratamento.Paciente))
            {
                return (Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada), null);
            }

            if (!tratamento.Paciente.EmAcompanhamento)
            {
                return (Resultado.Conflito("O paciente está inativo ou tem óbito registrado; não é possível cadastrar sessões."), null);
            }

            if (!StatusTratamento.Ativo(tratamento.Status))
            {
                return (Resultado.Invalido("Não é possível cadastrar sessões para um tratamento concluído ou inativado."), null);
            }

            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            return (null, new Contexto(tratamento, veterinario, clinica));
        }

        /// <summary>Sessões do plano já cadastradas: as canceladas e as inativadas não contam.</summary>
        private async Task<int> ContarCadastradas(Guid tratamentoId)
        {
            var contagens = await _sessoes.ContarPorTratamentos(new[] { tratamentoId });
            return contagens.TryGetValue(tratamentoId, out var contagem) ? contagem.Total : 0;
        }

        /// <summary>O plano só cresce: o previsto passa a ser o que foi cadastrado, se for mais.</summary>
        private static int NovoTotalPrevisto(int previstas, int cadastradas) =>
            Math.Min(MaximoDeSessoesPrevistas, Math.Max(previstas, cadastradas));

        /// <summary>Confere um intervalo isolado; devolve a mensagem do problema, ou null.</summary>
        private static string? ValidarIntervalo(DateTime inicio, DateTime fim, Clinica? clinica, bool conferirPassado)
        {
            if (fim <= inicio)
            {
                return "O término precisa ser depois do início da sessão.";
            }

            var relogio = RelogioDaClinica.Padrao;

            if (relogio.DiaDaClinica(inicio) != relogio.DiaDaClinica(fim.AddTicks(-1)))
            {
                return "A sessão precisa começar e terminar no mesmo dia.";
            }

            var duracao = (fim - inicio).TotalMinutes;

            if (duracao < DuracaoMinimaMinutos || duracao > DuracaoMaximaMinutos)
            {
                return $"A sessão deve durar de {DuracaoMinimaMinutos} minutos a {DuracaoMaximaMinutos / 60} horas.";
            }

            // HU-004, CA-5: o cadastro retroativo é bloqueado.
            if (conferirPassado && inicio <= DateTime.UtcNow)
            {
                return "Não é possível cadastrar sessões em datas ou horários passados.";
            }

            // HU-004, CA-1: o horário precisa estar dentro do funcionamento da clínica.
            if (clinica != null && !DentroDoExpediente(inicio, fim, clinica))
            {
                return $"A clínica atende das {clinica.HorarioAbertura:hh\\:mm} às {clinica.HorarioFechamento:hh\\:mm}. " +
                       "Escolha um horário dentro desse período.";
            }

            return null;
        }

        /// <summary>"09:00" ou "9:00" vira 09:00; qualquer outra coisa é recusada.</summary>
        private static bool LerHora(string? valor, out TimeSpan hora)
        {
            hora = default;

            if (string.IsNullOrWhiteSpace(valor) ||
                !TimeOnly.TryParseExact(valor.Trim(), new[] { "HH:mm", "H:mm", "HH:mm:ss" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var lida))
            {
                return false;
            }

            hora = lida.ToTimeSpan();
            return true;
        }

        /// <summary>Num cadastro com várias sessões, a mensagem diz de qual delas é o problema.</summary>
        private static string NaSessao(string mensagem, DateTime? inicio, int quantidade)
        {
            if (quantidade <= 1 || inicio == null)
            {
                return mensagem;
            }

            return $"Sessão de {Formatar(NormalizarParaUtc(inicio.Value))}: {char.ToLowerInvariant(mensagem[0])}{mensagem[1..]}";
        }

        private static string Formatar(DateTime instanteUtc) => RelogioDaClinica.Padrao.Formatar(instanteUtc);

        private static string Contar(int quantidade) => quantidade == 1 ? "1 sessão cadastrada" : $"{quantidade} sessões cadastradas";

        /// <summary>O expediente é definido no horário da clínica, não no do servidor.</summary>
        private static bool DentroDoExpediente(DateTime inicioUtc, DateTime fimUtc, Clinica clinica)
        {
            var relogio = RelogioDaClinica.Padrao;
            var inicio = relogio.ParaLocal(inicioUtc).TimeOfDay;
            var fim = relogio.ParaLocal(fimUtc).TimeOfDay;

            // Um término à meia-noite em ponto é o fim do dia, não o começo.
            if (fim == TimeSpan.Zero)
            {
                fim = TimeSpan.FromHours(24);
            }

            return inicio >= clinica.HorarioAbertura && fim <= clinica.HorarioFechamento;
        }

        /// <summary>Horários sem fuso são lidos como horário da clínica; com "Z" são UTC.</summary>
        public static DateTime NormalizarParaUtc(DateTime valor) => RelogioDaClinica.Padrao.NormalizarParaUtc(valor);

        public static SessaoDTO MapearParaDTO(Sessao sessao, int horasMinimasCancelamento, AtendimentoFisioterapeutico? atendimento)
        {
            return new SessaoDTO
            {
                Id = sessao.Id,
                TratamentoId = sessao.TratamentoId,
                NomeTratamento = sessao.Tratamento?.Nome ?? string.Empty,
                VeterinarioId = sessao.VeterinarioId,
                NomeVeterinario = sessao.Veterinario?.Usuario?.Nome ?? string.Empty,
                PacienteId = sessao.Tratamento?.PacienteId ?? Guid.Empty,
                NomePaciente = sessao.Tratamento?.Paciente?.Nome ?? string.Empty,
                NomeTutor = sessao.Tratamento?.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                DataHora = sessao.DataHora,
                DataHoraFim = sessao.DataHoraFim,
                Status = sessao.Status,
                Observacoes = sessao.Observacoes,
                Inativa = sessao.Inativa,
                Situacao = SituacaoSessao.De(sessao, atendimento != null, DateTime.UtcNow),
                PossuiAtendimento = atendimento != null,
                AtendimentoId = atendimento?.Id,
                NumeroSessao = atendimento?.NumeroSessao,
                EscalaDor = atendimento?.EscalaDor,
                TecnicasAplicadas = atendimento?.TecnicasAplicadas ?? string.Empty,
                PodeCancelar = PodeCancelar(sessao, horasMinimasCancelamento)
            };
        }

        /// <summary>RN-009: o cancelamento pelo tutor respeita a antecedência mínima da clínica.</summary>
        public static bool PodeCancelar(Sessao sessao, int horasMinimasCancelamento)
        {
            if (sessao.Inativa || StatusSessao.Encerrada(sessao.Status))
            {
                return false;
            }

            return sessao.DataHora - DateTime.UtcNow >= TimeSpan.FromHours(horasMinimasCancelamento);
        }
    }
}
