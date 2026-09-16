using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-014: troca de mensagens entre tutor e veterinário.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MensagensController : ControllerBase
    {
        private readonly MensagensUseCase _useCase;

        public MensagensController(MensagensUseCase useCase)
        {
            _useCase = useCase;
        }

        /// <summary>HU-014, CA-2: lista de conversas com status e contador de não lidas.</summary>
        [HttpGet("conversas")]
        public async Task<IActionResult> ListarConversas()
        {
            return this.Responder(await _useCase.ListarConversas());
        }

        /// <summary>HU-014, CA-3: abrir a conversa marca as mensagens recebidas como lidas.</summary>
        [HttpGet("conversa/{usuarioId:guid}")]
        public async Task<IActionResult> ObterConversa(Guid usuarioId, [FromQuery] bool marcarComoLida = true)
        {
            return this.Responder(await _useCase.ObterConversa(usuarioId, marcarComoLida));
        }

        [HttpGet("contatos")]
        public async Task<IActionResult> ListarContatos()
        {
            return this.Responder(await _useCase.ListarContatos());
        }

        [HttpGet("nao-lidas")]
        public async Task<IActionResult> ContarNaoLidas()
        {
            return this.Responder(await _useCase.ContarNaoLidas());
        }

        [HttpPost]
        public async Task<IActionResult> Enviar(EnviarMensagemDTO dto)
        {
            return this.ResponderCriado(await _useCase.Enviar(dto));
        }
    }
}
