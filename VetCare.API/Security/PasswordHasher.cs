using System.Security.Cryptography;

namespace VetCare.API.Security
{
    /// <summary>
    /// Hash de senha com PBKDF2 + salt aleatório por usuário, conforme a solução de
    /// Segurança da Tabela 2 do DAS ("senhas armazenadas com hash").
    /// Formato persistido: pbkdf2.{iteracoes}.{saltBase64}.{hashBase64}
    /// </summary>
    public class PasswordHasher
    {
        public const int TamanhoMinimoDaSenha = 6;
        public const int TamanhoMaximoDaSenha = 64;

        private const int Iteracoes = 210_000;
        private const int TamanhoSalt = 16;
        private const int TamanhoHash = 32;
        private const string Prefixo = "pbkdf2";

        public string Gerar(string senha)
        {
            var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
            var hash = Derivar(senha, salt, Iteracoes);

            return string.Join('.', Prefixo, Iteracoes, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        /// <summary>
        /// Valida a senha informada. <paramref name="precisaRehash"/> volta true quando o
        /// hash foi gerado com menos iterações do que o padrão atual, para que o chamador
        /// o regrave com a senha que acabou de ser confirmada.
        /// </summary>
        public bool Verificar(string senha, string hashArmazenado, out bool precisaRehash)
        {
            precisaRehash = false;

            if (string.IsNullOrWhiteSpace(hashArmazenado))
            {
                return false;
            }

            var partes = hashArmazenado.Split('.');

            if (partes.Length != 4 || partes[0] != Prefixo || !int.TryParse(partes[1], out var iteracoes) || iteracoes <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] hashEsperado;

            try
            {
                salt = Convert.FromBase64String(partes[2]);
                hashEsperado = Convert.FromBase64String(partes[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var hashCalculado = Derivar(senha, salt, iteracoes);
            var valido = CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);

            precisaRehash = valido && iteracoes < Iteracoes;
            return valido;
        }

        /// <summary>Mensagem de validação compartilhada por todos os pontos que recebem uma senha nova.</summary>
        public static string? ValidarForca(string? senha)
        {
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < TamanhoMinimoDaSenha)
            {
                return $"A senha deve ter no mínimo {TamanhoMinimoDaSenha} caracteres.";
            }

            if (senha.Length > TamanhoMaximoDaSenha)
            {
                return $"A senha deve ter no máximo {TamanhoMaximoDaSenha} caracteres.";
            }

            return null;
        }

        /// <summary>Senha provisória legível usada na redefinição pelo Administrador (HU-002, CA-5).</summary>
        public string GerarSenhaProvisoria()
        {
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var caracteres = new char[10];

            for (var i = 0; i < caracteres.Length; i++)
            {
                caracteres[i] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
            }

            return new string(caracteres);
        }

        private static byte[] Derivar(string senha, byte[] salt, int iteracoes)
        {
            return Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        }
    }
}
