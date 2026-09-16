using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-004, HU-005 e HU-006: agenda e confirmação de presença em sessão.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SessoesController : ControllerBase
    {
        private readonly AgendarSessaoUseCase _agendar;
        private readonly AtualizarStatusSessaoUseCase _atualizarStatus;
        private readonly ConsultarAgendaUseCase _consultarAgenda;
        private readonly UsuarioAtual _usuarioAtual;

        public SessoesController(
            AgendarSessaoUseCase agendar,
            AtualizarStatusSessaoUseCase atualizarStatus,
            ConsultarAgendaUseCase consultarAgenda,
            UsuarioAtual usuarioAtual)
        {
            _agendar = agendar;
            _atualizarStatus = atualizarStatus;
            _consultarAgenda = consultarAgenda;
            _usuarioAtual = usuarioAtual;
        }

        [HttpPost]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Agendar(AgendarSessaoDTO dto)
        {
            return this.ResponderCriado(await _agendar.Executar(dto));
        }

        /// <summary>
        /// HU-004, CA-3: agenda do veterinário nas visões Dia, Semana e Mês.
        /// A visão padrão é o dia, mantendo compatibilidade com chamadas antigas.
        /// </summary>
        [HttpGet("agenda/{veterinarioId:guid}")]
        public async Task<IActionResult> ConsultarAgenda(
            Guid veterinarioId,
            [FromQuery] DateTime? data,
            [FromQuery] string visao = "dia")
        {
            var referencia = data ?? DateTime.Now;
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

            var referencia = data ?? DateTime.Now;

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
            var referencia = data ?? DateTime.Now;
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
