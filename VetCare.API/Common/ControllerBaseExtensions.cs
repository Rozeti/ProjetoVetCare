using Microsoft.AspNetCore.Mvc;

namespace VetCare.API.Common
{
    /// <summary>
    /// Traduz o resultado de um caso de uso para a resposta HTTP correspondente,
    /// mantendo os controllers enxutos e as respostas de erro padronizadas.
    /// </summary>
    public static class ControllerBaseExtensions
    {
        /// <summary>
        /// Onde a mensagem de sucesso do caso de uso fica disponível para o
        /// <c>FiltroDeAtualizacoes</c> descrever a alteração às demais telas abertas.
        /// </summary>
        public const string ChaveDescricaoDaAtualizacao = "vetcare:descricao-da-atualizacao";

        public static IActionResult Responder(this ControllerBase controller, Resultado resultado)
        {
            if (!resultado.Sucesso)
            {
                return MapearFalha(controller, resultado);
            }

            RegistrarDescricao(controller, resultado.Mensagem);

            return controller.Ok(new { mensagem = resultado.Mensagem });
        }

        public static IActionResult Responder<T>(this ControllerBase controller, Resultado<T> resultado)
        {
            if (!resultado.Sucesso)
            {
                return MapearFalha(controller, resultado);
            }

            RegistrarDescricao(controller, resultado.Mensagem);

            return controller.Ok(resultado.Dados);
        }

        public static IActionResult ResponderCriado<T>(this ControllerBase controller, Resultado<T> resultado)
        {
            if (!resultado.Sucesso)
            {
                return MapearFalha(controller, resultado);
            }

            RegistrarDescricao(controller, resultado.Mensagem);

            return controller.StatusCode(StatusCodes.Status201Created, resultado.Dados);
        }

        private static void RegistrarDescricao(ControllerBase controller, string mensagem)
        {
            if (!string.IsNullOrWhiteSpace(mensagem))
            {
                controller.HttpContext.Items[ChaveDescricaoDaAtualizacao] = mensagem;
            }
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
