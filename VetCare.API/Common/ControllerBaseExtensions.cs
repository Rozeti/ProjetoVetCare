using Microsoft.AspNetCore.Mvc;

namespace VetCare.API.Common
{
    /// <summary>
    /// Traduz o resultado de um caso de uso para a resposta HTTP correspondente,
    /// mantendo os controllers enxutos e as respostas de erro padronizadas.
    /// </summary>
    public static class ControllerBaseExtensions
    {
        public static IActionResult Responder(this ControllerBase controller, Resultado resultado)
        {
            return resultado.Sucesso
                ? controller.Ok(new { mensagem = resultado.Mensagem })
                : MapearFalha(controller, resultado);
        }

        public static IActionResult Responder<T>(this ControllerBase controller, Resultado<T> resultado)
        {
            return resultado.Sucesso
                ? controller.Ok(resultado.Dados)
                : MapearFalha(controller, resultado);
        }

        public static IActionResult ResponderCriado<T>(this ControllerBase controller, Resultado<T> resultado)
        {
            return resultado.Sucesso
                ? controller.StatusCode(StatusCodes.Status201Created, resultado.Dados)
                : MapearFalha(controller, resultado);
        }

        private static IActionResult MapearFalha(ControllerBase controller, Resultado resultado)
        {
            var corpo = new { mensagem = resultado.Mensagem };

            return resultado.Falha switch
            {
                TipoFalha.NaoEncontrado => controller.NotFound(corpo),
                TipoFalha.Conflito => controller.Conflict(corpo),
                TipoFalha.NaoAutenticado => controller.Unauthorized(corpo),
                TipoFalha.NaoAutorizado => controller.StatusCode(StatusCodes.Status403Forbidden, corpo),
                TipoFalha.Bloqueado => controller.StatusCode(StatusCodes.Status423Locked, corpo),
                _ => controller.BadRequest(corpo)
            };
        }
    }
}
