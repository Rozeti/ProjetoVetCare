using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Configurações da clínica (tenant).</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClinicaController : ControllerBase
    {
        private readonly GerenciarClinicaUseCase _useCase;

        public ClinicaController(GerenciarClinicaUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> Obter()
        {
            return this.Responder(await _useCase.Obter());
        }

        [HttpPut]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Atualizar(AtualizarClinicaDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(dto));
        }
    }
}
