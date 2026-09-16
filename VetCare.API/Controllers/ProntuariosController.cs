using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-011: consulta do prontuário com linha do tempo, mídias e indicadores.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProntuariosController : ControllerBase
    {
        private readonly ConsultarProntuarioUseCase _consultar;

        public ProntuariosController(ConsultarProntuarioUseCase consultar)
        {
            _consultar = consultar;
        }

        /// <summary>
        /// O prontuário é criado sob demanda na primeira consulta, então não há endpoint
        /// separado de abertura: pedir o prontuário de um paciente sempre devolve um válido.
        /// </summary>
        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> Consultar(Guid pacienteId)
        {
            return this.Responder(await _consultar.Executar(pacienteId));
        }
    }
}
