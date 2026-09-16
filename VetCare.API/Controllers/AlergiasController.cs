using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Alergias, comorbidades e restrições do paciente.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlergiasController : ControllerBase
    {
        private readonly GerenciarAlergiasUseCase _useCase;

        public AlergiasController(GerenciarAlergiasUseCase useCase) => _useCase = useCase;

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId, [FromQuery] bool apenasAtivas = true)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId, apenasAtivas));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Registrar(CriarAlergiaDTO dto)
        {
            return this.ResponderCriado(await _useCase.Registrar(dto));
        }

        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> AlterarStatus(Guid id, [FromBody] AlterarStatusUsuarioDTO dto)
        {
            return this.Responder(await _useCase.AlterarStatus(id, dto.Ativo));
        }
    }
}
