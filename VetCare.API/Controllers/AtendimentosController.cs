using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-008: registro do atendimento fisioterapêutico.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
    public class AtendimentosController : ControllerBase
    {
        private readonly RegistrarAtendimentoUseCase _useCase;

        public AtendimentosController(RegistrarAtendimentoUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpPost]
        public async Task<IActionResult> Registrar(CriarAtendimentoDTO dto)
        {
            return this.ResponderCriado(await _useCase.Executar(dto));
        }

        /// <summary>
        /// Tela de evoluções: filtra por animal, tratamento, intervalo de datas, profissional e
        /// número da sessão. Com um tratamento informado, só as evoluções dele, em ordem cronológica.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] Guid? pacienteId,
            [FromQuery] Guid? tratamentoId,
            [FromQuery] DateTime? inicio,
            [FromQuery] DateTime? fim,
            [FromQuery] Guid? veterinarioId,
            [FromQuery] int? numeroSessao,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _useCase.Listar(
                pacienteId, tratamentoId, inicio, fim, veterinarioId, numeroSessao, paginacao));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        /// <summary>Permite à tela de sessão saber se já existe atendimento registrado.</summary>
        [HttpGet("sessao/{sessaoId:guid}")]
        public async Task<IActionResult> ObterPorSessao(Guid sessaoId)
        {
            return this.Responder(await _useCase.ObterPorSessao(sessaoId));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarAtendimentoDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }

        [HttpGet("{id:guid}/historico")]
        public async Task<IActionResult> ObterHistorico(Guid id)
        {
            return this.Responder(await _useCase.ObterHistorico(id));
        }
    }
}
