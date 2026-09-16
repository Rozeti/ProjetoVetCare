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
        public const string EmailAdministrador = "admin@vetcare.com";
        public const string SenhaPadrao = "vetcare123";

        public static async Task Executar(IServiceProvider provedor)
        {
            using var escopo = provedor.CreateScope();

            var context = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = escopo.ServiceProvider.GetRequiredService<PasswordHasher>();
            var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedInicial");

            var clinica = await GarantirClinica(context, logger);
            await GarantirAdministrador(context, clinica, hasher, logger);
        }

        private static async Task<Clinica> GarantirClinica(AppDbContext context, ILogger logger)
        {
            var clinica = await context.Clinicas.OrderBy(c => c.DataCadastro).FirstOrDefaultAsync();

            if (clinica != null)
            {
                return clinica;
            }

            clinica = new Clinica
            {
                Nome = "Clínica VetSPA",
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
            ILogger logger)
        {
            if (await context.Usuarios.AnyAsync(u => u.Perfil == Perfis.Administrador && u.Ativo))
            {
                return;
            }

            if (await context.Usuarios.AnyAsync(u => u.Email == EmailAdministrador))
            {
                logger.LogWarning(
                    "Nenhum administrador ativo encontrado, mas o e-mail {Email} já está em uso. " +
                    "Reative a conta diretamente no banco de dados.", EmailAdministrador);

                return;
            }

            await context.Usuarios.AddAsync(new Usuario
            {
                ClinicaId = clinica.Id,
                Nome = "Administrador",
                Email = EmailAdministrador,
                SenhaHash = hasher.Gerar(SenhaPadrao),
                Perfil = Perfis.Administrador,
                Ativo = true
            });

            await context.SaveChangesAsync();

            logger.LogWarning(
                "Conta de administrador criada: {Email} / senha '{Senha}'. Altere essa senha no primeiro acesso.",
                EmailAdministrador, SenhaPadrao);
        }
    }
}
