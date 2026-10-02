using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Envio por SMTP com MailKit, que é a biblioteca recomendada pela própria Microsoft
    /// no lugar do SmtpClient do .NET. Funciona com qualquer provedor: Gmail, Outlook,
    /// SendGrid, Brevo, Amazon SES ou o servidor da própria clínica.
    /// </summary>
    public class ServicoDeEmailSmtp : IServicoDeEmail
    {
        private readonly OpcoesDeEmail _opcoes;

        public ServicoDeEmailSmtp(IOptions<OpcoesDeEmail> opcoes)
        {
            _opcoes = opcoes.Value;
        }

        public string Descricao =>
            $"SMTP {_opcoes.Smtp.Host}:{_opcoes.Smtp.Porta} ({_opcoes.Smtp.Seguranca}), remetente {_opcoes.RemetenteEfetivo}";

        public bool EnviaDeVerdade => true;

        public async Task Enviar(MensagemDeEmail mensagem, CancellationToken cancelamento)
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_opcoes.NomeRemetente, _opcoes.RemetenteEfetivo));
            mime.To.Add(new MailboxAddress(mensagem.NomeDestinatario, mensagem.Destinatario));
            mime.Subject = mensagem.Assunto;

            mime.Body = new BodyBuilder
            {
                HtmlBody = mensagem.CorpoHtml,
                TextBody = mensagem.CorpoTexto
            }.ToMessageBody();

            using var cliente = CriarCliente();

            await ConectarEAutenticar(cliente, cancelamento);
            await cliente.SendAsync(mime, cancelamento);
            await cliente.DisconnectAsync(true, cancelamento);
        }

        /// <summary>Conecta e autentica sem enviar nada: é o bastante para saber se a configuração está certa.</summary>
        public async Task Verificar(CancellationToken cancelamento)
        {
            using var cliente = CriarCliente();

            await ConectarEAutenticar(cliente, cancelamento);
            await cliente.DisconnectAsync(true, cancelamento);
        }

        private SmtpClient CriarCliente() => new()
        {
            Timeout = (int)TimeSpan.FromSeconds(_opcoes.Smtp.TempoLimiteSegundos).TotalMilliseconds
        };

        private async Task ConectarEAutenticar(SmtpClient cliente, CancellationToken cancelamento)
        {
            await cliente.ConnectAsync(_opcoes.Smtp.Host, _opcoes.Smtp.Porta, ResolverSeguranca(), cancelamento);

            // Um relay interno da clínica (ou um servidor de testes) pode não pedir senha;
            // nesse caso o usuário informado é simplesmente ignorado em vez de derrubar o envio.
            var servidorAceitaLogin = cliente.Capabilities.HasFlag(SmtpCapabilities.Authentication);

            if (!string.IsNullOrWhiteSpace(_opcoes.Smtp.Usuario) && servidorAceitaLogin)
            {
                await cliente.AuthenticateAsync(_opcoes.Smtp.Usuario, _opcoes.Smtp.Senha, cancelamento);
            }
        }

        private SecureSocketOptions ResolverSeguranca() => _opcoes.Smtp.Seguranca.Trim().ToLowerInvariant() switch
        {
            "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "none" or "nenhuma" => SecureSocketOptions.None,
            "auto" => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.StartTls
        };
    }
}
