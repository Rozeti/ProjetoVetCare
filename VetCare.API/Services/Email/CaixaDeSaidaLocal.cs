using System.Text;
using Microsoft.Extensions.Options;

namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Substituto do SMTP enquanto nenhum servidor está configurado: cada e-mail vira um
    /// arquivo .html numa pasta da aplicação, que pode ser aberto no navegador. É assim
    /// que o fluxo inteiro — inclusive o link de redefinição de senha — pode ser testado
    /// numa máquina sem conta de e-mail. O conteúdo nunca vai para o log: links e
    /// códigos de senha não devem aparecer em arquivos de log, nem em desenvolvimento.
    /// </summary>
    public class CaixaDeSaidaLocal : IServicoDeEmail
    {
        private readonly string _pasta;
        private readonly ILogger<CaixaDeSaidaLocal> _logger;

        public CaixaDeSaidaLocal(
            IOptions<OpcoesDeEmail> opcoes,
            IWebHostEnvironment ambiente,
            ILogger<CaixaDeSaidaLocal> logger)
        {
            _pasta = Path.Combine(ambiente.ContentRootPath, opcoes.Value.PastaDaCaixaDeSaida);
            _logger = logger;
        }

        public string Descricao => $"caixa de saída local em {_pasta}";

        public async Task Enviar(MensagemDeEmail mensagem, CancellationToken cancelamento)
        {
            Directory.CreateDirectory(_pasta);

            var nome = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Simplificar(mensagem.Assunto)}.html";
            var caminho = Path.Combine(_pasta, nome);

            var conteudo = new StringBuilder()
                .AppendLine("<!-- VetCare: e-mail gravado localmente porque nenhum servidor SMTP está configurado. -->")
                .AppendLine($"<!-- Para: {mensagem.NomeDestinatario} <{mensagem.Destinatario}> -->")
                .AppendLine($"<!-- Assunto: {mensagem.Assunto} -->")
                .AppendLine($"<!-- Em: {DateTime.Now:dd/MM/yyyy HH:mm:ss} -->")
                .Append(mensagem.CorpoHtml)
                .ToString();

            await File.WriteAllTextAsync(caminho, conteudo, Encoding.UTF8, cancelamento);

            _logger.LogInformation(
                "E-mail \"{Assunto}\" para {Destinatario} gravado em {Caminho} (nenhum servidor SMTP configurado).",
                mensagem.Assunto, mensagem.Destinatario, caminho);
        }

        private static string Simplificar(string texto)
        {
            var simples = new string(texto
                .Normalize(NormalizationForm.FormD)
                .Where(c => char.IsLetterOrDigit(c) || c == ' ')
                .ToArray())
                .Trim()
                .Replace(' ', '-')
                .ToLowerInvariant();

            return simples.Length > 40 ? simples[..40] : simples;
        }
    }
}
