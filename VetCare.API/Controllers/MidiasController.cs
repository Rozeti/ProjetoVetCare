using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-010: fotos e vídeos anexados às sessões.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MidiasController : ControllerBase
    {
        private readonly AnexarMidiaUseCase _useCase;

        public MidiasController(AnexarMidiaUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<IActionResult> Anexar([FromForm] UploadMidiaDTO dto)
        {
            return this.ResponderCriado(await _useCase.Executar(dto));
        }

        [HttpGet("sessao/{sessaoId:guid}")]
        public async Task<IActionResult> ListarPorSessao(Guid sessaoId)
        {
            return this.Responder(await _useCase.ListarPorSessao(sessaoId));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Remover(Guid id)
        {
            return this.Responder(await _useCase.Remover(id));
        }
    }
}
