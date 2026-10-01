using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// HU-015: aparelhos que recebem as notificações no celular. O aplicativo chama estes
    /// endpoints sozinho — ao entrar e ao sair da conta; nenhuma tela os expõe diretamente.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DispositivosController : ControllerBase
    {
        private readonly DispositivosUseCase _useCase;

        public DispositivosController(DispositivosUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> ListarMeus()
        {
            return this.Responder(await _useCase.ListarMeus());
        }

        /// <summary>Registra (ou renova) o token de push do aparelho para o usuário autenticado.</summary>
        [HttpPost]
        public async Task<IActionResult> Registrar(RegistrarDispositivoDTO dto)
        {
            return this.Responder(await _useCase.Registrar(dto));
        }

        /// <summary>Chamado ao sair da conta: o aparelho para de receber avisos deste usuário.</summary>
        [HttpDelete]
        public async Task<IActionResult> Remover([FromQuery] string token)
        {
            return this.Responder(await _useCase.Remover(token));
        }
    }
}
