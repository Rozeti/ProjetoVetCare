namespace VetCare.API.Common
{
    /// <summary>
    /// Dados sobre a própria instalação que os e-mails precisam conhecer: o endereço do
    /// portal, para montar os links, e o nome pelo qual a clínica assina as mensagens.
    /// Seção <c>Aplicacao</c> do appsettings ou variáveis <c>Aplicacao__*</c>.
    /// </summary>
    public class OpcoesDaAplicacao
    {
        public const string Secao = "Aplicacao";

        /// <summary>Endereço público do portal web, sem barra no final. Base dos links enviados por e-mail.</summary>
        public string UrlPortal { get; set; } = "http://localhost:5173";

        public string NomeDoSistema { get; set; } = "VetCare";
        public string NomeDaClinica { get; set; } = "Clínica VetSPA";

        /// <summary>Monta um link absoluto para um caminho do portal, como "/minha-agenda".</summary>
        public string LinkDoPortal(string? caminho)
        {
            var baseUrl = UrlPortal.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(caminho))
            {
                return baseUrl;
            }

            return caminho.StartsWith('/') ? baseUrl + caminho : $"{baseUrl}/{caminho}";
        }
    }
}
