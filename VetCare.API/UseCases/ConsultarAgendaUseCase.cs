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
        private readonly IClinicaRepository _clinicas;
        private readonly UsuarioAtual _usuarioAtual;

        public ConsultarAgendaUseCase(
            ISessaoRepository sessoes,
            IVeterinarioRepository veterinarios,
            IAtendimentoRepository atendimentos,
            IClinicaRepository clinicas,
            UsuarioAtual usuarioAtual)
        {
            _sessoes = sessoes;
            _veterinarios = veterinarios;
            _atendimentos = atendimentos;
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
            var indiceCor = await ObterIndiceCor(veterinarioId);

            return Resultado<List<ItemAgendaDTO>>.Ok(await MapearItens(sessoes, indiceCor));
        }

        /// <summary>HU-005: agenda consolidada com todos os veterinários e o toggle de profissionais.</summary>
        public async Task<Resultado<AgendaGeralDTO>> ConsultarAgendaGeral(DateTime data, string visao, List<Guid>? veterinariosFiltrados)
        {
            var (inicio, fim) = CalcularIntervalo(data, visao);

            var veterinarios = await _veterinarios.Listar(_usuarioAtual.ClinicaId);
            var veterinariosDto = GerenciarVeterinariosUseCase.MapearLista(veterinarios);

            var sessoes = await _sessoes.ObterAgendaGeral(_usuarioAtual.ClinicaId, inicio, fim, veterinariosFiltrados);

            var cores = veterinariosDto.ToDictionary(v => v.Id, v => v.Cor);
            var itens = new List<ItemAgendaDTO>(sessoes.Count);

            foreach (var sessao in sessoes)
            {
                var item = await MapearItem(sessao);
                item.CorVeterinario = cores.TryGetValue(sessao.VeterinarioId, out var cor) ? cor : "#64748b";
                itens.Add(item);
            }

            var agenda = new AgendaGeralDTO
            {
                Inicio = inicio,
                Fim = fim,
                Veterinarios = veterinariosDto,
                Sessoes = itens
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

            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);
            var horasMinimas = clinica?.HorasMinimasCancelamento ?? 12;

            var sessoes = await _sessoes.ObterPorTutor(_usuarioAtual.TutorId.Value, apenasFuturas);
            var lista = new List<SessaoDTO>(sessoes.Count);

            foreach (var sessao in sessoes)
            {
                var possuiAtendimento = await _atendimentos.ObterPorSessao(sessao.Id) != null;
                lista.Add(AgendarSessaoUseCase.MapearParaDTO(sessao, horasMinimas, possuiAtendimento));
            }

            return Resultado<List<SessaoDTO>>.Ok(lista);
        }

        public async Task<Resultado<List<SessaoDTO>>> ConsultarPorTratamento(Guid tratamentoId)
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);
            var horasMinimas = clinica?.HorasMinimasCancelamento ?? 12;

            var sessoes = await _sessoes.ObterPorTratamento(tratamentoId);
            var lista = new List<SessaoDTO>(sessoes.Count);

            foreach (var sessao in sessoes)
            {
                var possuiAtendimento = await _atendimentos.ObterPorSessao(sessao.Id) != null;
                lista.Add(AgendarSessaoUseCase.MapearParaDTO(sessao, horasMinimas, possuiAtendimento));
            }

            return Resultado<List<SessaoDTO>>.Ok(lista);
        }

        /// <summary>
        /// Converte a visão escolhida em um intervalo [início, fim). O cálculo é feito em
        /// horário local e convertido para UTC, que é como as sessões são persistidas.
        /// </summary>
        public static (DateTime Inicio, DateTime Fim) CalcularIntervalo(DateTime data, string visao)
        {
            var referencia = DateTime.SpecifyKind(data.Date, DateTimeKind.Local);

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
                    inicioLocal = new DateTime(referencia.Year, referencia.Month, 1, 0, 0, 0, DateTimeKind.Local);
                    fimLocal = inicioLocal.AddMonths(1);
                    break;

                default:
                    inicioLocal = referencia;
                    fimLocal = referencia.AddDays(1);
                    break;
            }

            return (inicioLocal.ToUniversalTime(), fimLocal.ToUniversalTime());
        }

        private async Task<List<ItemAgendaDTO>> MapearItens(List<Sessao> sessoes, int indiceCor)
        {
            var cor = GerenciarVeterinariosUseCase.ObterCor(indiceCor);
            var itens = new List<ItemAgendaDTO>(sessoes.Count);

            foreach (var sessao in sessoes)
            {
                var item = await MapearItem(sessao);
                item.CorVeterinario = cor;
                itens.Add(item);
            }

            return itens;
        }

        private async Task<ItemAgendaDTO> MapearItem(Sessao sessao)
        {
            return new ItemAgendaDTO
            {
                SessaoId = sessao.Id,
                PacienteId = sessao.Tratamento?.PacienteId ?? Guid.Empty,
                TratamentoId = sessao.TratamentoId,
                VeterinarioId = sessao.VeterinarioId,
                DataHora = sessao.DataHora,
                NomePaciente = sessao.Tratamento?.Paciente?.Nome ?? "Paciente não informado",
                NomeTutor = sessao.Tratamento?.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                NomeVeterinario = sessao.Veterinario?.Usuario?.Nome ?? string.Empty,
                Status = sessao.Status,
                Observacoes = sessao.Observacoes,
                PossuiAtendimento = await _atendimentos.ObterPorSessao(sessao.Id) != null
            };
        }

        private async Task<int> ObterIndiceCor(Guid veterinarioId)
        {
            var veterinarios = await _veterinarios.Listar(_usuarioAtual.ClinicaId);
            var indice = veterinarios.FindIndex(v => v.Id == veterinarioId);

            return indice < 0 ? 0 : indice;
        }
    }
}
