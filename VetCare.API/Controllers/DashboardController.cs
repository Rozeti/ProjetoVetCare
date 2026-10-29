using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-016: indicadores do dia.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.EquipeClinica)]
    public class DashboardController : ControllerBase
    {
        private readonly ConsultarIndicadoresUseCase _indicadores;

        public DashboardController(ConsultarIndicadoresUseCase indicadores)
        {
            _indicadores = indicadores;
        }

        /// <summary>HU-016: indicadores operacionais do dia, com escopo definido pela RN-008.</summary>
        [HttpGet("indicadores")]
        public async Task<IActionResult> ObterIndicadores([FromQuery] DateTime? data)
        {
            return this.Responder(await _indicadores.Executar(data));
        }
    }
}
