using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VetCare.API.Models;

namespace VetCare.API.Security
{
    public class TokenService
    {
        /// <summary>Emissor e público padrão do JWT; configuráveis em Jwt:Emissor e Jwt:Publico.</summary>
        public const string EmissorPadrao = "VetCare";

        /// <summary>
        /// SHA-256 das chaves que já circularam em versões públicas do repositório (o antigo
        /// appsettings.json e o exemplo do .env). Quem as conhece assina tokens de qualquer
        /// perfil, então a API se recusa a usá-las. Só o hash fica aqui, para o texto não
        /// voltar a circular.
        /// </summary>
        private static readonly HashSet<string> ChavesPublicadas = new(StringComparer.OrdinalIgnoreCase)
        {
            "EBC9A016E9BE58D35F237C2C8A2E7E1CAF53B3CDFDACF637F5DF17C0B47A2CE9",
            "E44BD9A36607E0E67390F6F3BCD2978CCEA21B713284D87A79CEDA515C7EDB5D"
        };

        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Emite o JWT da sessão. Além da identificação do usuário, o token carrega a
        /// clínica (tenant) e o vínculo profissional, evitando uma consulta extra ao
        /// banco a cada requisição que precise filtrar por veterinário ou tutor.
        /// </summary>
        public string GerarToken(Usuario usuario, Guid? veterinarioId = null, Guid? tutorId = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var chave = Encoding.UTF8.GetBytes(ObterChave());

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Name, usuario.Nome),
                new(ClaimTypes.Email, usuario.Email),
                new(ClaimTypes.Role, usuario.Perfil),
                new(ClaimsPersonalizadas.ClinicaId, usuario.ClinicaId.ToString())
            };

            if (veterinarioId.HasValue)
            {
                claims.Add(new Claim(ClaimsPersonalizadas.VeterinarioId, veterinarioId.Value.ToString()));
            }

            if (tutorId.HasValue)
            {
                claims.Add(new Claim(ClaimsPersonalizadas.TutorId, tutorId.Value.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = Emissor,
                Audience = Publico,
                // RNF-002: expiração automática da sessão.
                Expires = DateTime.UtcNow.AddHours(HorasValidade),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(chave),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public int HorasValidade => _configuration.GetValue<int?>("Jwt:HorasValidade") ?? 8;

        public string Emissor => _configuration["Jwt:Emissor"] is { Length: > 0 } emissor ? emissor : EmissorPadrao;

        public string Publico => _configuration["Jwt:Publico"] is { Length: > 0 } publico ? publico : EmissorPadrao;

        public string ObterChave()
        {
            var chave = _configuration["Jwt:Chave"];

            if (ValidarChave(chave) is { } erro)
            {
                throw new InvalidOperationException(erro);
            }

            return chave!;
        }

        /// <summary>Motivo pelo qual a chave não pode assinar tokens, ou null quando ela serve.</summary>
        public static string? ValidarChave(string? chave)
        {
            if (string.IsNullOrWhiteSpace(chave) || Encoding.UTF8.GetByteCount(chave) < 32)
            {
                return "A chave JWT (Jwt:Chave) não está configurada ou tem menos de 32 bytes. Defina JWT_CHAVE no " +
                    ".env (Docker), Jwt:Chave com dotnet user-secrets (desenvolvimento) ou a variável de ambiente Jwt__Chave.";
            }

            if (ChavesPublicadas.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)))))
            {
                return "A chave JWT (Jwt:Chave) configurada já foi publicada no repositório e não protege nada. " +
                    "Troque-a por uma frase longa só sua; as sessões abertas com a chave antiga deixam de valer.";
            }

            return null;
        }
    }
}
