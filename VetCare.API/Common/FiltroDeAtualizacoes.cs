using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.Common
{
    /// <summary>
    /// Anuncia no <see cref="CentralDeAtualizacoes"/> toda requisição de escrita que deu
    /// certo. Ficar neste ponto — depois da ação, antes da resposta sair — é o que garante
    /// a cobertura pedida: qualquer endpoint que grave no banco passa por aqui, inclusive
    /// os que ainda serão escritos, sem que nenhum caso de uso precise se lembrar disso.
    ///
    /// O filtro apenas observa: ele nunca altera a resposta nem interrompe a requisição,
    /// e uma falha ao publicar é engolida para não derrubar uma operação já concluída.
    /// </summary>
    public class FiltroDeAtualizacoes : IAsyncActionFilter
    {
        private static readonly HashSet<string> MetodosDeEscrita =
            new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

        private readonly CentralDeAtualizacoes _central;
        private readonly UsuarioAtual _usuarioAtual;
        private readonly ILogger<FiltroDeAtualizacoes> _logger;

        public FiltroDeAtualizacoes(
            CentralDeAtualizacoes central,
            UsuarioAtual usuarioAtual,
            ILogger<FiltroDeAtualizacoes> logger)
        {
            _central = central;
            _usuarioAtual = usuarioAtual;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate proximo)
        {
            var executado = await proximo();

            try
            {
                Anunciar(contexto, executado);
            }
            catch (Exception excecao)
            {
                _logger.LogError(excecao, "Falha ao anunciar a atualização da requisição {Caminho}.",
                    contexto.HttpContext.Request.Path);
            }
        }

        private void Anunciar(ActionExecutingContext contexto, ActionExecutedContext executado)
        {
            var requisicao = contexto.HttpContext.Request;

            if (!MetodosDeEscrita.Contains(requisicao.Method) || executado.Exception != null)
            {
                return;
            }

            // Login e recuperação de senha entram aqui como POST anônimo. Sem clínica no
            // token não há para quem anunciar — e não se anuncia autenticação.
            if (_usuarioAtual.ClinicaId == Guid.Empty)
            {
                return;
            }

            if (!DeuCerto(executado.Result))
            {
                return;
            }

            var recurso = (executado.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;

            if (string.IsNullOrWhiteSpace(recurso))
            {
                return;
            }

            _central.Publicar(
                _usuarioAtual.ClinicaId,
                recurso.ToLowerInvariant(),
                AcaoDoMetodo(requisicao.Method),
                DescricaoDaResposta(contexto.HttpContext),
                _usuarioAtual.Id,
                _usuarioAtual.Nome);
        }

        /// <summary>
        /// Só 2xx conta como alteração efetiva: uma recusa de validação ou de permissão
        /// não mexeu no banco e não deve acordar tela nenhuma.
        /// </summary>
        private static bool DeuCerto(IActionResult? resultado)
        {
            var codigo = (resultado as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;

            return codigo is >= 200 and < 300;
        }

        private static string AcaoDoMetodo(string metodo) => metodo.ToUpperInvariant() switch
        {
            "POST" => "criado",
            "DELETE" => "removido",
            _ => "atualizado"
        };

        /// <summary>
        /// A mensagem que o caso de uso já escreveu para o usuário ("Presença confirmada",
        /// "Paciente cadastrado com sucesso") é a melhor descrição possível do evento, e
        /// os controllers a deixam aqui ao montar a resposta.
        /// </summary>
        private static string DescricaoDaResposta(HttpContext contexto)
        {
            return contexto.Items.TryGetValue(ControllerBaseExtensions.ChaveDescricaoDaAtualizacao, out var valor)
                   && valor is string descricao
                ? descricao
                : string.Empty;
        }
    }
}
