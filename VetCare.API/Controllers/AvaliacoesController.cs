using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-007: registro e correção da avaliação clínica.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
    public class AvaliacoesController : ControllerBase
    {
        private readonly RegistrarAvaliacaoUseCase _useCase;

        public AvaliacoesController(RegistrarAvaliacaoUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpPost]
        public async Task<IActionResult> Registrar(CriarAvaliacaoDTO dto)
        {
            return this.ResponderCriado(await _useCase.Executar(dto));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        /// <summary>HU-007, CA-4: correção preservando a rastreabilidade da versão anterior.</summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarAvaliacaoDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }

        /// <summary>RN-004: versões anteriores arquivadas do registro.</summary>
        [HttpGet("{id:guid}/historico")]
        public async Task<IActionResult> ObterHistorico(Guid id)
        {
            return this.Responder(await _useCase.ObterHistorico(id));
        }
    }
}
