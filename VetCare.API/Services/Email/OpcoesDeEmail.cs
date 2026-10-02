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

        /// <summary>Remetente de fábrica: um endereço fictício que nenhum provedor aceita como real.</summary>
        public const string RemetentePadrao = "nao-responda@vetcare.local";

        public string Remetente { get; set; } = RemetentePadrao;
        public string NomeRemetente { get; set; } = "VetCare";

        public OpcoesSmtp Smtp { get; set; } = new();

        /// <summary>Pasta onde os e-mails ficam quando não há SMTP, relativa à raiz da aplicação.</summary>
        public string PastaDaCaixaDeSaida { get; set; } = "emails-enviados";

        public bool SmtpConfigurado => !string.IsNullOrWhiteSpace(Smtp.Host);

        /// <summary>
        /// Endereço que de fato assina os e-mails. Gmail, Outlook e Brevo só aceitam enviar
        /// em nome da própria conta autenticada; por isso, quando o remetente ficou no valor
        /// de fábrica (ou em branco) e o usuário do SMTP é um e-mail, ele é usado como
        /// remetente. Assim basta preencher servidor, usuário e senha para funcionar.
        /// </summary>
        public string RemetenteEfetivo
        {
            get
            {
                var remetente = Remetente?.Trim() ?? string.Empty;
                var usuarioSmtp = Smtp.Usuario?.Trim() ?? string.Empty;

                var remetenteGenerico = remetente.Length == 0
                    || remetente.Equals(RemetentePadrao, StringComparison.OrdinalIgnoreCase)
                    || remetente.EndsWith("@vetcare.local", StringComparison.OrdinalIgnoreCase);

                if (remetenteGenerico && SmtpConfigurado && PareceEmail(usuarioSmtp))
                {
                    return usuarioSmtp;
                }

                return remetente.Length == 0 ? RemetentePadrao : remetente;
            }
        }

        private static bool PareceEmail(string valor)
        {
            var arroba = valor.IndexOf('@');
            return arroba > 0 && arroba < valor.Length - 1 && !valor.Contains(' ');
        }

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
