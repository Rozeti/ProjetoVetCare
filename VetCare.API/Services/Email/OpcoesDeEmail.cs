namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Seção <c>Email</c> do appsettings (ou variáveis <c>Email__Smtp__Host</c> etc.). Sem
    /// um servidor SMTP configurado a API não falha: os e-mails são gravados em disco,
    /// o que basta para desenvolver e demonstrar o fluxo sem conta de e-mail.
    /// </summary>
    public class OpcoesDeEmail
    {
        public const string Secao = "Email";

        public string Remetente { get; set; } = "nao-responda@vetcare.local";
        public string NomeRemetente { get; set; } = "VetCare";

        public OpcoesSmtp Smtp { get; set; } = new();

        /// <summary>Pasta onde os e-mails ficam quando não há SMTP, relativa à raiz da aplicação.</summary>
        public string PastaDaCaixaDeSaida { get; set; } = "emails-enviados";

        public bool SmtpConfigurado => !string.IsNullOrWhiteSpace(Smtp.Host);

        public class OpcoesSmtp
        {
            public string Host { get; set; } = string.Empty;
            public int Porta { get; set; } = 587;
            public string Usuario { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;

            /// <summary>Auto, StartTls, Ssl ou None. StartTls é o padrão da porta 587 (Gmail, Outlook, SendGrid, Brevo).</summary>
            public string Seguranca { get; set; } = "StartTls";

            public int TempoLimiteSegundos { get; set; } = 20;
        }
    }
}
