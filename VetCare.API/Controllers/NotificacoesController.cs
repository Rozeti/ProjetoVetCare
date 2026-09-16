using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-015: notificações in-app do usuário autenticado.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificacoesController : ControllerBase
    {
        private readonly NotificacoesUseCase _useCase;

        public NotificacoesController(NotificacoesUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] bool apenasNaoVisualizadas = false,
            [FromQuery] int limite = 50)
        {
            return this.Responder(await _useCase.Listar(apenasNaoVisualizadas, limite));
        }

        [HttpGet("nao-visualizadas")]
        public async Task<IActionResult> ContarNaoVisualizadas()
        {
            return this.Responder(await _useCase.ContarNaoVisualizadas());
        }

        [HttpPatch("{id:guid}/visualizada")]
        public async Task<IActionResult> MarcarComoVisualizada(Guid id)
        {
            return this.Responder(await _useCase.MarcarComoVisualizada(id));
        }

        [HttpPatch("todas/visualizadas")]
        public async Task<IActionResult> MarcarTodasComoVisualizadas()
        {
            return this.Responder(await _useCase.MarcarTodasComoVisualizadas());
        }
    }
}
