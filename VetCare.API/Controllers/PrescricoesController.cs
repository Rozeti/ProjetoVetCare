using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Receituário do paciente.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PrescricoesController : ControllerBase
    {
        private readonly GerenciarPrescricoesUseCase _useCase;

        public PrescricoesController(GerenciarPrescricoesUseCase useCase) => _useCase = useCase;

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId));
        }

        /// <summary>Dados completos usados na impressão da receita.</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Emitir(CriarPrescricaoDTO dto)
        {
            return this.ResponderCriado(await _useCase.Emitir(dto));
        }

        /// <summary>RN-004: a receita é cancelada, não excluída.</summary>
        [HttpPatch("{id:guid}/cancelar")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Cancelar(Guid id, [FromBody] AtualizarStatusSessaoDTO dto)
        {
            return this.Responder(await _useCase.Cancelar(id, dto.Motivo));
        }
    }
}
