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
    public class VeterinariosController : ControllerBase
    {
        private readonly GerenciarVeterinariosUseCase _useCase;

        public VeterinariosController(GerenciarVeterinariosUseCase useCase)
        {
            _useCase = useCase;
        }

        /// <summary>Usada também pelo toggle de profissionais da agenda geral (HU-005, CA-2).</summary>
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            return this.Responder(await _useCase.Listar());
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Cadastrar(CriarVeterinarioDTO dto)
        {
            return this.ResponderCriado(await _useCase.Cadastrar(dto));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarVeterinarioDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }
    }
}
