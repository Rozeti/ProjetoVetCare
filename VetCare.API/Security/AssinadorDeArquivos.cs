using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VetCare.API.Security
{
    /// <summary>
    /// Mídias e documentos clínicos não ficam em pasta pública: cada URL entregue ao
    /// cliente carrega uma assinatura com prazo, verificada pelo <c>ArquivosController</c>.
    /// Assim a foto de um paciente só abre para quem recebeu o endereço de uma resposta
    /// autenticada e autorizada — e a tag &lt;img&gt;, que não envia cabeçalhos, continua
    /// funcionando. Quem tiver acesso ao banco não consegue montar um link válido sem a chave.
    /// </summary>
    public class AssinadorDeArquivos
    {
        public const string PastaDeMidias = "uploads";
        public const string PastaDeDocumentos = "documentos";

        /// <summary>Tempo em que o link continua abrindo depois de a tela ter sido carregada.</summary>
        public static readonly TimeSpan Validade = TimeSpan.FromHours(4);

        private static readonly Regex NomeSeguro = new("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\.[a-z0-9]{2,5}$",
            RegexOptions.Compiled);

        private readonly byte[] _chave;

        public AssinadorDeArquivos(IConfiguration configuracao)
        {
            var chave = configuracao["Jwt:Chave"] ?? string.Empty;

            // A chave do JWT já é o segredo da instalação; derivar outra a partir dela
            // evita um segundo segredo para configurar sem reaproveitar o mesmo material.
            _chave = SHA256.HashData(Encoding.UTF8.GetBytes("arquivos:" + chave));
        }

        public static bool PastaConhecida(string pasta) => pasta is PastaDeMidias or PastaDeDocumentos;

        public static bool NomeValido(string nome) => NomeSeguro.IsMatch(nome);

        /// <summary>
        /// Transforma o caminho guardado no banco ("/uploads/x.jpg") no endereço assinado
        /// que o cliente usa. Caminhos absolutos (armazenamento externo) passam intactos.
        /// </summary>
        public string Assinar(string caminhoRelativo)
        {
            if (string.IsNullOrWhiteSpace(caminhoRelativo) || caminhoRelativo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return caminhoRelativo;
            }

            var partes = caminhoRelativo.Trim('/').Split('/', 2);

            if (partes.Length != 2 || !PastaConhecida(partes[0]) || !NomeValido(partes[1]))
            {
                return caminhoRelativo;
            }

            var expira = DateTimeOffset.UtcNow.Add(Validade).ToUnixTimeSeconds();
            var assinatura = Calcular(partes[0], partes[1], expira);

            return $"/api/arquivos/{partes[0]}/{partes[1]}?exp={expira}&sig={assinatura}";
        }

        public bool Validar(string pasta, string nome, long expira, string? assinatura)
        {
            if (string.IsNullOrWhiteSpace(assinatura) || !PastaConhecida(pasta) || !NomeValido(nome))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
            {
                return false;
            }

            var esperada = Encoding.UTF8.GetBytes(Calcular(pasta, nome, expira));
            var recebida = Encoding.UTF8.GetBytes(assinatura);

            return esperada.Length == recebida.Length && CryptographicOperations.FixedTimeEquals(esperada, recebida);
        }

        private string Calcular(string pasta, string nome, long expira)
        {
            var dados = Encoding.UTF8.GetBytes($"{pasta}/{nome}:{expira.ToString(CultureInfo.InvariantCulture)}");
            return Convert.ToHexString(HMACSHA256.HashData(_chave, dados)).ToLowerInvariant();
        }
    }
}
