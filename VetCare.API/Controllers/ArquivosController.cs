using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// Entrega as mídias das sessões e os documentos do prontuário. Não há listagem nem
    /// navegação: o arquivo só sai para quem apresenta a URL assinada que a API entregou
    /// numa resposta autorizada, e dentro do prazo da assinatura (RNF-002, RN-003).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ArquivosController : ControllerBase
    {
        private static readonly FileExtensionContentTypeProvider TiposDeConteudo = new();

        private readonly AssinadorDeArquivos _assinador;
        private readonly ArmazenamentoArquivos _armazenamento;

        public ArquivosController(AssinadorDeArquivos assinador, ArmazenamentoArquivos armazenamento)
        {
            _assinador = assinador;
            _armazenamento = armazenamento;
        }

        /// <summary>Anônimo por desenho: a autorização está na assinatura, não no token.</summary>
        [HttpGet("{pasta}/{nome}")]
        [AllowAnonymous]
        public IActionResult Baixar(string pasta, string nome, [FromQuery] long exp, [FromQuery] string? sig)
        {
            if (!_assinador.Validar(pasta, nome, exp, sig))
            {
                return NotFound(new { mensagem = "Link inválido ou expirado. Recarregue a tela para gerar um novo." });
            }

            var caminho = _armazenamento.ResolverCaminhoFisico($"/{pasta}/{nome}");

            if (caminho == null)
            {
                return NotFound(new { mensagem = "Arquivo não encontrado." });
            }

            if (!TiposDeConteudo.TryGetContentType(nome, out var tipo))
            {
                tipo = "application/octet-stream";
            }

            // Links assinados expiram, então o navegador pode guardar o arquivo por um tempo.
            Response.Headers.CacheControl = "private, max-age=3600";

            return PhysicalFile(caminho, tipo, enableRangeProcessing: true);
        }
    }
}
