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
    public class TutoresController : ControllerBase
    {
        private readonly GerenciarTutoresUseCase _useCase;

        public TutoresController(GerenciarTutoresUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Listar(
            [FromQuery] string? busca,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _useCase.Listar(busca, paginacao));
        }

        /// <summary>Lista completa para os seletores de tutor nos formulários.</summary>
        [HttpGet("selecao")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> ListarParaSelecao()
        {
            return this.Responder(await _useCase.ListarParaSelecao());
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Cadastrar(CriarTutorDTO dto)
        {
            return this.ResponderCriado(await _useCase.Cadastrar(dto));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarTutorDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }
    }
}
