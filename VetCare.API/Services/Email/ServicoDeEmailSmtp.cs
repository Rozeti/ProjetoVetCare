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

        public string Descricao => $"SMTP {_opcoes.Smtp.Host}:{_opcoes.Smtp.Porta} ({_opcoes.Smtp.Seguranca})";

        public async Task Enviar(MensagemDeEmail mensagem, CancellationToken cancelamento)
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_opcoes.NomeRemetente, _opcoes.Remetente));
            mime.To.Add(new MailboxAddress(mensagem.NomeDestinatario, mensagem.Destinatario));
            mime.Subject = mensagem.Assunto;

            mime.Body = new BodyBuilder
            {
                HtmlBody = mensagem.CorpoHtml,
                TextBody = mensagem.CorpoTexto
            }.ToMessageBody();

            using var cliente = new SmtpClient
            {
                Timeout = (int)TimeSpan.FromSeconds(_opcoes.Smtp.TempoLimiteSegundos).TotalMilliseconds
            };

            await cliente.ConnectAsync(_opcoes.Smtp.Host, _opcoes.Smtp.Porta, ResolverSeguranca(), cancelamento);

            if (!string.IsNullOrWhiteSpace(_opcoes.Smtp.Usuario))
            {
                await cliente.AuthenticateAsync(_opcoes.Smtp.Usuario, _opcoes.Smtp.Senha, cancelamento);
            }

            await cliente.SendAsync(mime, cancelamento);
            await cliente.DisconnectAsync(true, cancelamento);
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
