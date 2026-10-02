using System.Net.Sockets;
using MailKit.Security;

namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Traduz as exceções do envio por SMTP para uma frase que diz o que fazer. As mensagens
    /// originais dos provedores são em inglês e cheias de códigos ("535-5.7.8 Username and
    /// Password not accepted"); quem configura o sistema precisa saber que isso quer dizer
    /// "senha de app errada", e não "o sistema quebrou".
    /// </summary>
    public static class FalhasDeEmail
    {
        public static string Descrever(Exception excecao)
        {
            var raiz = Raiz(excecao);

            return raiz switch
            {
                AuthenticationException => "O servidor de e-mail recusou o usuário ou a senha. " +
                    "No Gmail e no Outlook, a senha precisa ser uma \"senha de app\" (não a senha normal da conta), " +
                    "e a verificação em duas etapas precisa estar ativa.",

                SocketException or TimeoutException => "Não foi possível conectar ao servidor de e-mail. " +
                    "Confira o endereço (SMTP_HOST), a porta (SMTP_PORTA) e se a rede permite a conexão.",

                SslHandshakeException => "A conexão segura com o servidor de e-mail falhou. " +
                    "Confira SMTP_SEGURANCA: use StartTls na porta 587 e Ssl na porta 465.",

                MailKit.ServiceNotAuthenticatedException => "O servidor exige autenticação. Preencha SMTP_USUARIO e SMTP_SENHA.",

                MailKit.ServiceNotConnectedException => "A conexão com o servidor de e-mail caiu antes do envio. Tente novamente.",

                MailKit.Net.Smtp.SmtpCommandException comando => DescreverRecusa(comando),

                MailKit.Net.Smtp.SmtpProtocolException => "O servidor de e-mail respondeu de forma inesperada. " +
                    "Confira a porta e o modo de segurança (StartTls na 587, Ssl na 465).",

                OperationCanceledException => "O envio foi interrompido antes de terminar.",

                _ => $"Falha no envio: {raiz.Message}"
            };
        }

        private static string DescreverRecusa(MailKit.Net.Smtp.SmtpCommandException comando)
        {
            var motivo = string.IsNullOrWhiteSpace(comando.Message) ? "sem detalhes" : comando.Message.Trim();

            return comando.ErrorCode switch
            {
                MailKit.Net.Smtp.SmtpErrorCode.SenderNotAccepted =>
                    "O servidor não aceitou o remetente. Use em EMAIL_REMETENTE o mesmo endereço da conta " +
                    $"(ou um remetente validado no provedor). Resposta do servidor: {motivo}",

                MailKit.Net.Smtp.SmtpErrorCode.RecipientNotAccepted =>
                    $"O servidor não aceitou o destinatário. Resposta do servidor: {motivo}",

                MailKit.Net.Smtp.SmtpErrorCode.MessageNotAccepted =>
                    $"O servidor recusou a mensagem. Resposta do servidor: {motivo}",

                _ => $"O servidor de e-mail recusou o comando. Resposta do servidor: {motivo}"
            };
        }

        /// <summary>A causa útil costuma estar embrulhada em outra exceção (ex.: SocketException dentro de IOException).</summary>
        private static Exception Raiz(Exception excecao)
        {
            var atual = excecao;

            while (atual.InnerException != null && atual is not AuthenticationException
                   && atual is not MailKit.Net.Smtp.SmtpCommandException)
            {
                atual = atual.InnerException;
            }

            return atual;
        }
    }
}
