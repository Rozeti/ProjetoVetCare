using System.Security.Claims;

namespace VetCare.API.Security
{
    /// <summary>
    /// Expõe os dados do usuário autenticado a partir das claims do JWT, para que os
    /// casos de uso apliquem a RN-005 (controle de acesso por perfil) sem depender do
    /// HttpContext diretamente.
    /// </summary>
    public class UsuarioAtual
    {
        private readonly IHttpContextAccessor _accessor;

        public UsuarioAtual(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

        public bool Autenticado => Principal?.Identity?.IsAuthenticated == true;

        public Guid Id
        {
            get
            {
                var valor = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.TryParse(valor, out var id) ? id : Guid.Empty;
            }
        }

        public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        public string Nome => Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

        public string Perfil => Principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        public Guid ClinicaId
        {
            get
            {
                var valor = Principal?.FindFirstValue(ClaimsPersonalizadas.ClinicaId);
                return Guid.TryParse(valor, out var id) ? id : Guid.Empty;
            }
        }

        /// <summary>Id do registro Veterinario, presente apenas quando o perfil é Veterinario.</summary>
        public Guid? VeterinarioId
        {
            get
            {
                var valor = Principal?.FindFirstValue(ClaimsPersonalizadas.VeterinarioId);
                return Guid.TryParse(valor, out var id) ? id : null;
            }
        }

        /// <summary>Id do registro Tutor, presente apenas quando o perfil é Tutor.</summary>
        public Guid? TutorId
        {
            get
            {
                var valor = Principal?.FindFirstValue(ClaimsPersonalizadas.TutorId);
                return Guid.TryParse(valor, out var id) ? id : null;
            }
        }

        public bool EhAdministrador => Perfil == Perfis.Administrador;
        public bool EhVeterinario => Perfil == Perfis.Veterinario;
        public bool EhTutor => Perfil == Perfis.Tutor;
        public bool EhApoio => Perfil == Perfis.Apoio;

        /// <summary>RN-003: somente Administrador e Veterinário enxergam observações internas.</summary>
        public bool PodeVerObservacoesInternas => EhAdministrador || EhVeterinario;
    }

    public static class ClaimsPersonalizadas
    {
        public const string ClinicaId = "clinica_id";
        public const string VeterinarioId = "veterinario_id";
        public const string TutorId = "tutor_id";
    }
}
