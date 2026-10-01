using System.Security.Cryptography;
using System.Text;

namespace VetCare.API.Security
{
    /// <summary>
    /// Geração e verificação das credenciais de uso único usadas para definir uma senha:
    /// o token longo do link e o código curto digitado no aplicativo. Nada aqui é
    /// reversível — o banco só conhece os hashes.
    /// </summary>
    public static class CredenciaisTemporarias
    {
        public const int TamanhoDoCodigo = 6;

        /// <summary>256 bits aleatórios em Base64 segura para URL: impossível de adivinhar.</summary>
        public static string GerarToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

        /// <summary>
        /// Seis dígitos, fáceis de digitar no celular. A proteção contra tentativa e erro
        /// vem do limite de tentativas por pedido e da validade curta, não do tamanho.
        /// </summary>
        public static string GerarCodigo()
        {
            var numero = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return numero.ToString().PadLeft(TamanhoDoCodigo, '0');
        }

        public static string HashDoToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        /// <summary>
        /// O código é curto demais para um hash puro resistir a quem tenha a tabela em
        /// mãos; misturar o identificador do pedido obriga a atacar um registro de cada vez.
        /// </summary>
        public static string HashDoCodigo(Guid pedidoId, string codigo) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{pedidoId:N}:{NormalizarCodigo(codigo)}")));

        public static bool CodigoConfere(Guid pedidoId, string codigoInformado, string hashArmazenado)
        {
            var calculado = Encoding.UTF8.GetBytes(HashDoCodigo(pedidoId, codigoInformado));
            var esperado = Encoding.UTF8.GetBytes(hashArmazenado);

            return calculado.Length == esperado.Length && CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }

        /// <summary>Aceita "123 456" ou "123-456" como o mesmo código: pessoas copiam do e-mail de vários jeitos.</summary>
        public static string NormalizarCodigo(string codigo) =>
            new(codigo.Where(char.IsDigit).ToArray());
    }
}
