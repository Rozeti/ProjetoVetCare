using Microsoft.AspNetCore.Diagnostics;

namespace VetCare.API.Common
{
    /// <summary>
    /// Converte qualquer exceção não tratada na mesma resposta { mensagem } usada pelo
    /// restante da API, registrando o erro completo no log. O cliente recebe um
    /// identificador para citar no suporte, mas nunca a pilha de execução.
    /// </summary>
    public class TratamentoDeErros : IExceptionHandler
    {
        private readonly ILogger<TratamentoDeErros> _logger;

        public TratamentoDeErros(ILogger<TratamentoDeErros> logger) => _logger = logger;

        public async ValueTask<bool> TryHandleAsync(
            HttpContext contexto,
            Exception excecao,
            CancellationToken cancellationToken)
        {
            var identificador = contexto.TraceIdentifier;

            _logger.LogError(
                excecao,
                "Falha não tratada em {Metodo} {Caminho}. Identificador: {Identificador}",
                contexto.Request.Method,
                contexto.Request.Path,
                identificador);

            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await contexto.Response.WriteAsJsonAsync(
                new
                {
                    mensagem = "Ocorreu um erro inesperado. Se o problema persistir, informe o código abaixo ao suporte.",
                    identificador
                },
                cancellationToken);

            return true;
        }
    }
}
