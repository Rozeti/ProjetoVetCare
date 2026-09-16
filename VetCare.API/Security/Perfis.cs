namespace VetCare.API.Security
{
    /// <summary>
    /// Os quatro perfis de uso descritos no Escopo do DAS. Centralizados aqui para que
    /// os atributos [Authorize(Roles = ...)] não dependam de literais espalhados.
    /// </summary>
    public static class Perfis
    {
        public const string Administrador = "Administrador";
        public const string Veterinario = "Veterinario";
        public const string Tutor = "Tutor";
        public const string Apoio = "Apoio";

        public const string AdministradorOuApoio = Administrador + "," + Apoio;
        public const string AdministradorOuVeterinario = Administrador + "," + Veterinario;
        public const string EquipeClinica = Administrador + "," + Veterinario + "," + Apoio;
        public const string Todos = Administrador + "," + Veterinario + "," + Tutor + "," + Apoio;

        public static readonly string[] Validos =
        {
            Administrador, Veterinario, Tutor, Apoio
        };

        public static bool EhValido(string? perfil)
        {
            return !string.IsNullOrWhiteSpace(perfil)
                   && Validos.Contains(perfil, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Normaliza a grafia informada para o valor canônico do perfil.</summary>
        public static string Normalizar(string perfil)
        {
            return Validos.FirstOrDefault(p => string.Equals(p, perfil, StringComparison.OrdinalIgnoreCase))
                   ?? perfil;
        }
    }
}
