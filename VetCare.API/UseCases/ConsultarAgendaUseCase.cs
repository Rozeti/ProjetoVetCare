using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-004 e HU-005: agenda individual do veterinário nas visões Dia, Semana e Mês,
    /// e agenda geral consolidada da clínica.
    /// </summary>
    public class ConsultarAgendaUseCase
    {
        private readonly ISessaoRepository _sessoes;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IAtendimentoRepository _atendimentos;
        private readonly ITratamentoRepository _tratamentos;
        private readonly IClinicaRepository _clinicas;
        private readonly UsuarioAtual _usuarioAtual;

        public ConsultarAgendaUseCase(
            ISessaoRepository sessoes,
            IVeterinarioRepository veterinarios,
            IAtendimentoRepository atendimentos,
            ITratamentoRepository tratamentos,
            IClinicaRepository clinicas,
            UsuarioAtual usuarioAtual)
        {
            _sessoes = sessoes;
            _veterinarios = veterinarios;
            _atendimentos = atendimentos;
            _tratamentos = tratamentos;
            _clinicas = clinicas;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>Visões Dia, Semana e Mês da agenda de um veterinário (HU-004, CA-3).</summary>
        public async Task<Resultado<List<ItemAgendaDTO>>> ConsultarPorVisao(Guid veterinarioId, DateTime data, string visao)
        {
            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<ItemAgendaDTO>>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            var (inicio, fim) = CalcularIntervalo(data, visao);
            var sessoes = await _sessoes.ObterPorPeriodo(veterinarioId, inicio, fim);
            var cor = GerenciarVeterinariosUseCase.ObterCor(veterinarioId);

            return Resultado<List<ItemAgendaDTO>>.Ok(await MapearItens(sessoes, _ => cor));
        }

        /// <summary>HU-005: agenda consolidada com todos os veterinários e o toggle de profissionais.</summary>
        public async Task<Resultado<AgendaGeralDTO>> ConsultarAgendaGeral(DateTime data, string visao, List<Guid>? veterinariosFiltrados)
        {
            var (inicio, fim) = CalcularIntervalo(data, visao);

            var veterinarios = await _veterinarios.Listar(_usuarioAtual.ClinicaId);
            var veterinariosDto = GerenciarVeterinariosUseCase.MapearLista(veterinarios);

            var sessoes = await _sessoes.ObterAgendaGeral(_usuarioAtual.ClinicaId, inicio, fim, veterinariosFiltrados);

            var agenda = new AgendaGeralDTO
            {
                Inicio = inicio,
                Fim = fim,
                Veterinarios = veterinariosDto,
                Sessoes = await MapearItens(sessoes, GerenciarVeterinariosUseCase.ObterCor)
            };

            return Resultado<AgendaGeralDTO>.Ok(agenda);
        }

        /// <summary>HU-013, CA-3: agenda dos pets do tutor autenticado.</summary>
        public async Task<Resultado<List<SessaoDTO>>> ConsultarAgendaDoTutor(bool apenasFuturas)
        {
            if (_usuarioAtual.TutorId == null)
            {
                return Resultado<List<SessaoDTO>>.NaoEncontrado("Cadastro de tutor não encontrado para este usuário.");
            }

            var sessoes = await _sessoes.ObterPorTutor(_usuarioAtual.TutorId.Value, apenasFuturas);

            return Resultado<List<SessaoDTO>>.Ok(await MapearSessoes(sessoes));
        }

        /// <summary>Sessões de um tratamento, para a equipe e para o tutor do paciente.</summary>
        public async Task<Resultado<List<SessaoDTO>>> ConsultarPorTratamento(Guid tratamentoId)
        {
            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(tratamentoId);

            if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<SessaoDTO>>.NaoEncontrado("Tratamento não encontrado.");
            }

            // HU-013, CA-1: o tutor só vê as sessões dos próprios pets; o veterinário, as dos seus pacientes.
            if (!AcessoAoPaciente.Permitido(_usuarioAtual, tratamento.Paciente))
            {
                return Resultado<List<SessaoDTO>>.NaoAutorizado("Você não tem acesso a este tratamento.");
            }

            var sessoes = await _sessoes.ObterPorTratamento(tratamentoId);

            // A consulta por tratamento não carrega o paciente; ele já está em mãos.
            foreach (var sessao in sessoes)
            {
                sessao.Tratamento = tratamento;
            }

            return Resultado<List<SessaoDTO>>.Ok(await MapearSessoes(sessoes));
        }

        /// <summary>
        /// Converte a visão escolhida em um intervalo [início, fim). O cálculo é feito no
        /// horário da clínica e convertido para UTC, que é como as sessões são persistidas.
        /// </summary>
        public static (DateTime Inicio, DateTime Fim) CalcularIntervalo(DateTime data, string visao)
        {
            var relogio = RelogioDaClinica.Padrao;
            var referencia = relogio.DiaDaClinica(data);

            DateTime inicioLocal;
            DateTime fimLocal;

            switch ((visao ?? "dia").Trim().ToLowerInvariant())
            {
                case "semana":
                    // A semana começa no domingo, como no calendário da interface.
                    inicioLocal = referencia.AddDays(-(int)referencia.DayOfWeek);
                    fimLocal = inicioLocal.AddDays(7);
                    break;

                case "mes":
                case "mês":
                    inicioLocal = new DateTime(referencia.Year, referencia.Month, 1);
                    fimLocal = inicioLocal.AddMonths(1);
                    break;

                default:
                    inicioLocal = referencia;
                    fimLocal = referencia.AddDays(1);
                    break;
            }

            return (relogio.ParaUtc(inicioLocal), relogio.ParaUtc(fimLocal));
        }

        private async Task<List<SessaoDTO>> MapearSessoes(List<Sessao> sessoes)
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);
            var horasMinimas = clinica?.HorasMinimasCancelamento ?? 12;

            var comAtendimento = await _atendimentos.ObterSessoesComAtendimento(sessoes.Select(s => s.Id));

            return sessoes
                .Select(sessao => AgendarSessaoUseCase.MapearParaDTO(sessao, horasMinimas, comAtendimento.Contains(sessao.Id)))
                .ToList();
        }

        /// <summary>Uma única consulta descobre quais sessões já têm atendimento, qualquer que seja o tamanho da agenda.</summary>
        private async Task<List<ItemAgendaDTO>> MapearItens(List<Sessao> sessoes, Func<Guid, string> corDoVeterinario)
        {
            var comAtendimento = await _atendimentos.ObterSessoesComAtendimento(sessoes.Select(s => s.Id));

            return sessoes.Select(sessao => new ItemAgendaDTO
            {
                SessaoId = sessao.Id,
                PacienteId = sessao.Tratamento?.PacienteId ?? Guid.Empty,
                TratamentoId = sessao.TratamentoId,
                VeterinarioId = sessao.VeterinarioId,
                DataHora = sessao.DataHora,
                NomePaciente = sessao.Tratamento?.Paciente?.Nome ?? "Paciente não informado",
                NomeTutor = sessao.Tratamento?.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                NomeVeterinario = sessao.Veterinario?.Usuario?.Nome ?? string.Empty,
                CorVeterinario = corDoVeterinario(sessao.VeterinarioId),
                Status = sessao.Status,
                Observacoes = sessao.Observacoes,
                PossuiAtendimento = comAtendimento.Contains(sessao.Id)
            }).ToList();
        }
    }
}
