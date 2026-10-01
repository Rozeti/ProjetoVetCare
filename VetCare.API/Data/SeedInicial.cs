using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.Data
{
    /// <summary>
    /// Garante o mínimo necessário para o sistema ser utilizável na primeira execução:
    /// a clínica e uma conta de administrador. Nada além disso é criado, para que a
    /// base comece limpa e receba apenas os dados reais da clínica.
    /// </summary>
    public static class SeedInicial
    {
        /// <summary>Usados quando <c>Seed:EmailAdministrador</c> e <c>Seed:SenhaAdministrador</c> não estão configurados.</summary>
        public const string EmailAdministradorPadrao = "admin@vetcare.com";
        public const string SenhaAdministradorPadrao = "vetcare123";

        public static async Task Executar(IServiceProvider provedor)
        {
            using var escopo = provedor.CreateScope();

            var context = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = escopo.ServiceProvider.GetRequiredService<PasswordHasher>();
            var configuracao = escopo.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedInicial");

            var clinica = await GarantirClinica(context, configuracao, logger);
            await GarantirAdministrador(context, clinica, hasher, configuracao, logger);
        }

        private static async Task<Clinica> GarantirClinica(AppDbContext context, IConfiguration configuracao, ILogger logger)
        {
            var clinica = await context.Clinicas.OrderBy(c => c.DataCadastro).FirstOrDefaultAsync();

            if (clinica != null)
            {
                return clinica;
            }

            clinica = new Clinica
            {
                Nome = configuracao["Aplicacao:NomeDaClinica"] is { Length: > 0 } nome ? nome : "Clínica VetSPA",
                Telefone = string.Empty,
                Endereco = string.Empty
            };

            await context.Clinicas.AddAsync(clinica);
            await context.SaveChangesAsync();

            logger.LogInformation("Clínica inicial criada. Ajuste os dados em Configurações.");

            return clinica;
        }

        /// <summary>
        /// Sem um administrador ninguém consegue cadastrar usuários (HU-002), o que
        /// deixaria o sistema inacessível. Por isso a conta é recriada sempre que
        /// nenhuma existir, e não apenas no primeiro start.
        /// </summary>
        private static async Task GarantirAdministrador(
            AppDbContext context,
            Clinica clinica,
            PasswordHasher hasher,
            IConfiguration configuracao,
            ILogger logger)
        {
            if (await context.Usuarios.AnyAsync(u => u.Perfil == Perfis.Administrador && u.Ativo))
            {
                return;
            }

            var email = (configuracao["Seed:EmailAdministrador"] is { Length: > 0 } e ? e : EmailAdministradorPadrao)
                .Trim()
                .ToLowerInvariant();

            var senha = configuracao["Seed:SenhaAdministrador"] is { Length: > 0 } s ? s : SenhaAdministradorPadrao;
            var senhaEhPadrao = senha == SenhaAdministradorPadrao;

            if (await context.Usuarios.AnyAsync(u => u.Email == email))
            {
                logger.LogWarning(
                    "Nenhum administrador ativo encontrado, mas o e-mail {Email} já está em uso. " +
                    "Reative a conta diretamente no banco de dados.", email);

                return;
            }

            await context.Usuarios.AddAsync(new Usuario
            {
                ClinicaId = clinica.Id,
                Nome = "Administrador",
                Email = email,
                SenhaHash = hasher.Gerar(senha),
                Perfil = Perfis.Administrador,
                Ativo = true
            });

            await context.SaveChangesAsync();

            // A senha nunca vai para o log: a padrão está na documentação, e a configurada
            // só quem a definiu conhece.
            logger.LogWarning(
                "Conta de administrador criada para {Email} {Origem}. Altere a senha no primeiro acesso.",
                email,
                senhaEhPadrao ? "com a senha padrão da documentação" : "com a senha definida em Seed:SenhaAdministrador");
        }
    }
}
