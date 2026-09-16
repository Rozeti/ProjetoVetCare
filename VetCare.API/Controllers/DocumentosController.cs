using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-012: documentos clínicos do prontuário.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentosController : ControllerBase
    {
        private readonly GerenciarDocumentosUseCase _useCase;

        public DocumentosController(GerenciarDocumentosUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpPost]
        [Authorize(Roles = Perfis.EquipeClinica)]
        [RequestSizeLimit(15 * 1024 * 1024)]
        public async Task<IActionResult> Anexar([FromForm] UploadDocumentoDTO dto)
        {
            return this.ResponderCriado(await _useCase.Anexar(dto));
        }

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId));
        }

        /// <summary>HU-012, CA-2: visualização e download pelo usuário autorizado.</summary>
        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Baixar(Guid id)
        {
            var resultado = await _useCase.ObterParaDownload(id);

            if (!resultado.Sucesso)
            {
                return this.Responder(resultado);
            }

            var (caminho, nomeArquivo) = resultado.Dados;
            var conteudo = await System.IO.File.ReadAllBytesAsync(caminho);

            return File(conteudo, "application/octet-stream", nomeArquivo);
        }
    }
}
