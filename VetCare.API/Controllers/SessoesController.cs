using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// HU-004, HU-005 e HU-006: cadastro das sessões dos tratamentos, agenda, confirmação de
    /// presença e a gestão das sessões (situação, inativação e reativação).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SessoesController : ControllerBase
    {
        private readonly CadastrarSessoesUseCase _cadastrar;
        private readonly GerenciarSessoesUseCase _gerenciar;
        private readonly AtualizarStatusSessaoUseCase _atualizarStatus;
        private readonly ConsultarAgendaUseCase _consultarAgenda;
        private readonly UsuarioAtual _usuarioAtual;

        public SessoesController(
            CadastrarSessoesUseCase cadastrar,
            GerenciarSessoesUseCase gerenciar,
            AtualizarStatusSessaoUseCase atualizarStatus,
            ConsultarAgendaUseCase consultarAgenda,
            UsuarioAtual usuarioAtual)
        {
            _cadastrar = cadastrar;
            _gerenciar = gerenciar;
            _atualizarStatus = atualizarStatus;
            _consultarAgenda = consultarAgenda;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>Cadastra uma ou várias sessões de um tratamento, cada uma com início e fim.</summary>
        [HttpPost]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Cadastrar(CadastrarSessoesDTO dto)
        {
            return this.ResponderCriado(await _cadastrar.Executar(dto));
        }

        /// <summary>
        /// Prévia de um plano de sessões: a quantidade, os dias da semana e o horário viram datas,
        /// pulando as que não cabem na agenda. Nada é gravado, por isso é um GET
        /// (<c>?diasDaSemana=1&amp;diasDaSemana=3</c>).
        /// </summary>
        [HttpGet("previa")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Previa([FromQuery] PlanoDeSessoesDTO dto)
        {
            return this.Responder(await _cadastrar.Previa(dto));
        }

        /// <summary>
        /// Gestão das sessões da clínica, com filtro por animal, tratamento, veterinário, período e
        /// situação (Agendada, Aguardando registro, Realizada, Cancelada ou Inativada).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Listar(
            [FromQuery] Guid? pacienteId,
            [FromQuery] Guid? tratamentoId,
            [FromQuery] Guid? veterinarioId,
            [FromQuery] DateTime? inicio,
            [FromQuery] DateTime? fim,
            [FromQuery] string? situacao,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _gerenciar.Listar(pacienteId, tratamentoId, veterinarioId, inicio, fim, situacao, paginacao));
        }

        /// <summary>Retira a sessão do plano; ela fica no histórico e pode ser reativada.</summary>
        [HttpPatch("{id:guid}/inativar")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Inativar(Guid id, [FromBody] InativarSessaoDTO? dto)
        {
            return this.Responder(await _gerenciar.Inativar(id, dto ?? new InativarSessaoDTO()));
        }

        [HttpPatch("{id:guid}/reativar")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Reativar(Guid id)
        {
            return this.Responder(await _gerenciar.Reativar(id));
        }

        /// <summary>
        /// HU-004, CA-3: agenda do veterinário nas visões Dia, Semana e Mês. A agenda de um
        /// profissional lista pacientes e tutores de toda a clínica, por isso é reservada à equipe.
        /// </summary>
        [HttpGet("agenda/{veterinarioId:guid}")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> ConsultarAgenda(
            Guid veterinarioId,
            [FromQuery] DateTime? data,
            [FromQuery] string visao = "dia")
        {
            var referencia = data ?? RelogioDaClinica.Padrao.Agora;
            return this.Responder(await _consultarAgenda.ConsultarPorVisao(veterinarioId, referencia, visao));
        }

        /// <summary>Atalho para o veterinário autenticado consultar a própria agenda.</summary>
        [HttpGet("agenda/minha")]
        [Authorize(Roles = Perfis.Veterinario)]
        public async Task<IActionResult> ConsultarMinhaAgenda(
            [FromQuery] DateTime? data,
            [FromQuery] string visao = "dia")
        {
            if (_usuarioAtual.VeterinarioId == null)
            {
                return NotFound(new { mensagem = "Cadastro de veterinário não encontrado para este usuário." });
            }

            var referencia = data ?? RelogioDaClinica.Padrao.Agora;

            return this.Responder(
                await _consultarAgenda.ConsultarPorVisao(_usuarioAtual.VeterinarioId.Value, referencia, visao));
        }

        /// <summary>HU-005: agenda geral consolidada, restrita a Administrador e apoio (RN-008).</summary>
        [HttpGet("agenda-geral")]
        [Authorize(Roles = Perfis.AdministradorOuApoio)]
        public async Task<IActionResult> ConsultarAgendaGeral(
            [FromQuery] DateTime? data,
            [FromQuery] string visao = "dia",
            [FromQuery] string? veterinarios = null)
        {
            var referencia = data ?? RelogioDaClinica.Padrao.Agora;
            var filtrados = ConverterListaDeIds(veterinarios);

            return this.Responder(await _consultarAgenda.ConsultarAgendaGeral(referencia, visao, filtrados));
        }

        /// <summary>HU-013, CA-3: sessões dos pets do tutor autenticado.</summary>
        [HttpGet("minhas")]
        [Authorize(Roles = Perfis.Tutor)]
        public async Task<IActionResult> ListarMinhasSessoes([FromQuery] bool apenasFuturas = false)
        {
            return this.Responder(await _consultarAgenda.ConsultarAgendaDoTutor(apenasFuturas));
        }

        /// <summary>Sessões de um tratamento; o tutor só alcança os tratamentos dos próprios pets.</summary>
        [HttpGet("tratamento/{tratamentoId:guid}")]
        public async Task<IActionResult> ListarPorTratamento(Guid tratamentoId)
        {
            return this.Responder(await _consultarAgenda.ConsultarPorTratamento(tratamentoId));
        }

        /// <summary>HU-006: confirmação ou cancelamento de presença.</summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusSessaoDTO dto)
        {
            return this.Responder(await _atualizarStatus.Executar(id, dto));
        }

        private static List<Guid>? ConverterListaDeIds(string? valores)
        {
            if (string.IsNullOrWhiteSpace(valores))
            {
                return null;
            }

            var ids = valores
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => Guid.TryParse(v.Trim(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            return ids.Count > 0 ? ids : null;
        }
    }
}
