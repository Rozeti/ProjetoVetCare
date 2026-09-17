using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// Canal por onde as telas abertas ficam sabendo que o banco mudou. O cliente chama
    /// este endpoint em laço, sempre informando a última versão que recebeu; a requisição
    /// fica pendurada até surgir novidade ou até o prazo curto acabar. Assim a confirmação
    /// de presença feita pelo tutor chega à agenda do veterinário, do apoio e do
    /// administrativo em segundos, e o mesmo vale para cancelamentos, novos pacientes e
    /// qualquer outra gravação.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AtualizacoesController : ControllerBase
    {
        /// <summary>
        /// Teto da espera. Precisa ser confortavelmente menor que o tempo limite dos
        /// clientes e que o de proxies no caminho, senão a conexão cai antes da resposta.
        /// </summary>
        private const int EsperaMaximaEmSegundos = 30;

        private readonly CentralDeAtualizacoes _central;
        private readonly UsuarioAtual _usuarioAtual;

        public AtualizacoesController(CentralDeAtualizacoes central, UsuarioAtual usuarioAtual)
        {
            _central = central;
            _usuarioAtual = usuarioAtual;
        }

        /// <param name="desde">
        /// Última versão conhecida pelo cliente. Omitir (ou enviar um número negativo)
        /// significa primeira conexão: a resposta volta na hora, apenas com a versão atual.
        /// </param>
        /// <param name="espera">Segundos que a requisição pode ficar pendurada.</param>
        [HttpGet]
        public async Task<ActionResult<FeedAtualizacoesDTO>> Consultar(
            [FromQuery] long desde = -1,
            [FromQuery] int espera = 25,
            CancellationToken cancelamento = default)
        {
            var segundos = Math.Clamp(espera, 0, EsperaMaximaEmSegundos);

            var leitura = await _central.Aguardar(
                _usuarioAtual.ClinicaId,
                desde,
                TimeSpan.FromSeconds(segundos),
                cancelamento);

            // RN-003 e HU-013, CA-1: o tutor divide a clínica com outros tutores, então o
            // aviso que chega até ele não carrega texto nem autor — só o recurso que mudou.
            // Ele recarrega os próprios dados, e a API devolve apenas o que é dele.
            var ehTutor = _usuarioAtual.EhTutor;

            return Ok(new FeedAtualizacoesDTO
            {
                Versao = leitura.Versao,
                Reiniciar = leitura.Reiniciar,
                Eventos = leitura.Eventos.Select(evento => new EventoAtualizacaoDTO
                {
                    Versao = evento.Versao,
                    Recurso = evento.Recurso,
                    Acao = evento.Acao,
                    Descricao = ehTutor ? string.Empty : evento.Descricao,
                    Autor = ehTutor ? string.Empty : evento.Autor,
                    Propria = evento.AutorId != Guid.Empty && evento.AutorId == _usuarioAtual.Id,
                    Em = evento.Em
                }).ToList()
            });
        }
    }
}
