using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Pedidos de exame gerados a partir de uma avaliação.</summary>
    [ApiController]
    [Route("api/pedidos-exame")]
    [Authorize]
    public class PedidosExameController : ControllerBase
    {
        private readonly GerenciarPedidosExameUseCase _useCase;

        public PedidosExameController(GerenciarPedidosExameUseCase useCase) => _useCase = useCase;

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId));
        }

        /// <summary>Dados completos usados na impressão do pedido.</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Emitir(CriarPedidoExameDTO dto)
        {
            return this.ResponderCriado(await _useCase.Emitir(dto));
        }

        /// <summary>RN-004: o pedido é cancelado, não excluído.</summary>
        [HttpPatch("{id:guid}/cancelar")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarPedidoExameDTO dto)
        {
            return this.Responder(await _useCase.Cancelar(id, dto.Motivo));
        }
    }
}
