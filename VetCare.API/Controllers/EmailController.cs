using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.Security;
using VetCare.API.Services.Email;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// Situação do envio de e-mails e envio de teste. Só o Administrador vê isto: o nome do
    /// servidor e o remetente são detalhes de infraestrutura da clínica.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.Administrador)]
    public class EmailController : ControllerBase
    {
        private readonly DiagnosticoDeEmail _diagnostico;

        public EmailController(DiagnosticoDeEmail diagnostico)
        {
            _diagnostico = diagnostico;
        }

        [HttpGet]
        public IActionResult Situacao()
        {
            return Ok(_diagnostico.Situacao());
        }

        /// <summary>Envia um e-mail de teste para a conta do próprio administrador e responde na hora se deu certo.</summary>
        [HttpPost("teste")]
        public async Task<IActionResult> EnviarTeste(CancellationToken cancelamento)
        {
            return this.Responder(await _diagnostico.EnviarTeste(cancelamento));
        }
    }
}
