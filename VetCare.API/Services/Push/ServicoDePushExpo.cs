using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace VetCare.API.Services.Push
{
    /// <summary>
    /// Entrega as notificações pelo serviço de push da Expo, que por sua vez fala com o
    /// Firebase (Android) e o APNs (iOS). O aplicativo do tutor registra o token do
    /// aparelho no login; aqui só mandamos a mensagem para esse token.
    /// Contrato: https://docs.expo.dev/push-notifications/sending-notifications/
    /// </summary>
    public class ServicoDePushExpo : IServicoDePush
    {
        public const string NomeDoCliente = "expo-push";

        /// <summary>Limite do serviço da Expo por requisição.</summary>
        private const int TamanhoDoLote = 100;

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IHttpClientFactory _clientes;
        private readonly OpcoesDePush _opcoes;
        private readonly ILogger<ServicoDePushExpo> _logger;

        public ServicoDePushExpo(IHttpClientFactory clientes, IOptions<OpcoesDePush> opcoes, ILogger<ServicoDePushExpo> logger)
        {
            _clientes = clientes;
            _opcoes = opcoes.Value;
            _logger = logger;
        }

        public bool Habilitado => _opcoes.Habilitado;

        /// <summary>Formato que a Expo emite: ExponentPushToken[xxxxxxxx] ou ExpoPushToken[xxxxxxxx].</summary>
        public static bool TokenTemFormatoValido(string? token) =>
            !string.IsNullOrWhiteSpace(token) &&
            (token.StartsWith("ExponentPushToken[", StringComparison.Ordinal) ||
             token.StartsWith("ExpoPushToken[", StringComparison.Ordinal)) &&
            token.EndsWith(']');

        public async Task<IReadOnlyList<ResultadoPush>> Enviar(
            IReadOnlyList<MensagemPush> mensagens,
            CancellationToken cancelamento)
        {
            var resultados = new List<ResultadoPush>(mensagens.Count);

            foreach (var lote in mensagens.Chunk(TamanhoDoLote))
            {
                resultados.AddRange(await EnviarLote(lote, cancelamento));
            }

            return resultados;
        }

        private async Task<IEnumerable<ResultadoPush>> EnviarLote(MensagemPush[] lote, CancellationToken cancelamento)
        {
            var corpo = lote.Select(m => new RequisicaoExpo
            {
                To = m.TokenPush,
                Title = m.Titulo,
                Body = m.Corpo,
                Data = m.Dados,
                Sound = "default",
                ChannelId = "default",
                Priority = "high"
            }).ToList();

            try
            {
                var cliente = _clientes.CreateClient(NomeDoCliente);

                using var requisicao = new HttpRequestMessage(HttpMethod.Post, _opcoes.Expo.Url)
                {
                    Content = JsonContent.Create(corpo, options: Json)
                };

                if (!string.IsNullOrWhiteSpace(_opcoes.Expo.TokenDeAcesso))
                {
                    requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opcoes.Expo.TokenDeAcesso);
                }

                using var resposta = await cliente.SendAsync(requisicao, cancelamento);
                var texto = await resposta.Content.ReadAsStringAsync(cancelamento);

                if (!resposta.IsSuccessStatusCode)
                {
                    var erro = $"Serviço de push respondeu {(int)resposta.StatusCode}: {Resumir(texto)}";
                    _logger.LogWarning("{Erro}", erro);
                    return lote.Select(m => new ResultadoPush(m.TokenPush, false, erro, false));
                }

                var retorno = JsonSerializer.Deserialize<RespostaExpo>(texto, Json);

                if (retorno?.Data == null || retorno.Data.Count != lote.Length)
                {
                    var erro = $"Resposta inesperada do serviço de push: {Resumir(texto)}";
                    _logger.LogWarning("{Erro}", erro);
                    return lote.Select(m => new ResultadoPush(m.TokenPush, false, erro, false));
                }

                return lote.Select((mensagem, indice) => Interpretar(mensagem, retorno.Data[indice]));
            }
            catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(excecao, "Não foi possível falar com o serviço de push da Expo.");
                return lote.Select(m => new ResultadoPush(m.TokenPush, false, excecao.Message, false));
            }
        }

        private static ResultadoPush Interpretar(MensagemPush mensagem, TicketExpo ticket)
        {
            if (string.Equals(ticket.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new ResultadoPush(mensagem.TokenPush, true, null, false);
            }

            var codigo = ticket.Details?.Error ?? string.Empty;
            var invalido = codigo is "DeviceNotRegistered" or "InvalidCredentials" or "MismatchSenderId";

            return new ResultadoPush(
                mensagem.TokenPush,
                false,
                string.IsNullOrWhiteSpace(codigo) ? ticket.Message ?? "Falha no envio" : $"{codigo}: {ticket.Message}",
                invalido && codigo == "DeviceNotRegistered");
        }

        private static string Resumir(string texto) => texto.Length > 300 ? texto[..300] + "..." : texto;

        private sealed class RequisicaoExpo
        {
            public string To { get; set; } = string.Empty;
            public string? Title { get; set; }
            public string? Body { get; set; }
            public IReadOnlyDictionary<string, string>? Data { get; set; }
            public string? Sound { get; set; }
            public string? ChannelId { get; set; }
            public string? Priority { get; set; }
        }

        private sealed class RespostaExpo
        {
            public List<TicketExpo>? Data { get; set; }
        }

        private sealed class TicketExpo
        {
            public string? Status { get; set; }
            public string? Id { get; set; }
            public string? Message { get; set; }
            public DetalhesExpo? Details { get; set; }
        }

        private sealed class DetalhesExpo
        {
            public string? Error { get; set; }
        }
    }
}
