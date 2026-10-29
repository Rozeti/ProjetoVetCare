using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// Trilha de auditoria da clínica: quem criou, alterou, inativou ou excluiu cada dado.
    /// A consulta fica restrita ao Administrador.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.Administrador)]
    public class AuditoriaController : ControllerBase
    {
        private readonly ConsultarAuditoriaUseCase _useCase;

        public AuditoriaController(ConsultarAuditoriaUseCase useCase) => _useCase = useCase;

        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] Guid? usuarioId,
            [FromQuery] string? acao,
            [FromQuery] string? entidade,
            [FromQuery] DateTime? inicio,
            [FromQuery] DateTime? fim,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _useCase.Listar(usuarioId, acao, entidade, inicio, fim, paginacao));
        }
    }
}
