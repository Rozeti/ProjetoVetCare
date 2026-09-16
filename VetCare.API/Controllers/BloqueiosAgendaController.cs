using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Períodos de indisponibilidade do veterinário na agenda.</summary>
    [ApiController]
    [Route("api/bloqueios-agenda")]
    [Authorize(Roles = Perfis.EquipeClinica)]
    public class BloqueiosAgendaController : ControllerBase
    {
        private readonly GerenciarBloqueiosAgendaUseCase _useCase;

        public BloqueiosAgendaController(GerenciarBloqueiosAgendaUseCase useCase) => _useCase = useCase;

        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] Guid? veterinarioId,
            [FromQuery] DateTime? inicio,
            [FromQuery] DateTime? fim)
        {
            // Sem período informado, a consulta cobre os próximos 90 dias.
            var de = inicio ?? DateTime.UtcNow.Date;
            var ate = fim ?? de.AddDays(90);

            return this.Responder(await _useCase.Listar(veterinarioId, de, ate));
        }

        [HttpPost]
        public async Task<IActionResult> Criar(CriarBloqueioAgendaDTO dto)
        {
            return this.ResponderCriado(await _useCase.Criar(dto));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Remover(Guid id)
        {
            return this.Responder(await _useCase.Remover(id));
        }
    }
}
