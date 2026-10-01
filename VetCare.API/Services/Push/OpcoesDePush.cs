namespace VetCare.API.Services.Push
{
    /// <summary>Seção <c>Push</c> do appsettings. O serviço da Expo não exige credenciais para começar.</summary>
    public class OpcoesDePush
    {
        public const string Secao = "Push";

        public bool Habilitado { get; set; } = true;

        public OpcoesExpo Expo { get; set; } = new();

        public class OpcoesExpo
        {
            public string Url { get; set; } = "https://exp.host/--/api/v2/push/send";

            /// <summary>
            /// Só é necessário se a opção "Enhanced Push Security" estiver ligada no painel da
            /// Expo; sem ela o envio funciona com o endereço público.
            /// </summary>
            public string TokenDeAcesso { get; set; } = string.Empty;
        }
    }
}
