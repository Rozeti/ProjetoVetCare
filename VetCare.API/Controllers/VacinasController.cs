using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Carteira de vacinação e vermifugação dos pacientes.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VacinasController : ControllerBase
    {
        private readonly GerenciarVacinasUseCase _useCase;

        public VacinasController(GerenciarVacinasUseCase useCase) => _useCase = useCase;

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId));
        }

        /// <summary>Painel de prevenção: doses que vencem na janela informada.</summary>
        [HttpGet("vencendo")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> ListarVencendo([FromQuery] int dias = 30)
        {
            return this.Responder(await _useCase.ListarVencendo(dias));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Registrar(CriarVacinaDTO dto)
        {
            return this.ResponderCriado(await _useCase.Registrar(dto));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarVacinaDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Remover(Guid id)
        {
            return this.Responder(await _useCase.Remover(id));
        }
    }
}
