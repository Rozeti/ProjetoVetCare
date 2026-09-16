using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VetCare.API.Models;

namespace VetCare.API.Security
{
    public class TokenService
    {
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

        public string ObterChave()
        {
            var chave = _configuration["Jwt:Chave"];

            if (string.IsNullOrWhiteSpace(chave) || Encoding.UTF8.GetByteCount(chave) < 32)
            {
                throw new InvalidOperationException(
                    "A chave JWT (Jwt:Chave) não está configurada ou tem menos de 32 bytes. " +
                    "Defina um valor seguro em appsettings.json ou na variável de ambiente Jwt__Chave.");
            }

            return chave;
        }
    }
}
