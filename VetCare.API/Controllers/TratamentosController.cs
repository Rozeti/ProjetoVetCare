using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TratamentosController : ControllerBase
    {
        private readonly GerenciarTratamentosUseCase _useCase;

        public TratamentosController(GerenciarTratamentosUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId));
        }

        [HttpGet("meus")]
        [Authorize(Roles = Perfis.Veterinario)]
        public async Task<IActionResult> ListarMeus()
        {
            return this.Responder(await _useCase.ListarDoVeterinario(null));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Cadastrar(CriarTratamentoDTO dto)
        {
            return this.ResponderCriado(await _useCase.Cadastrar(dto));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarTratamentoDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }
    }
}
