using System.ComponentModel.DataAnnotations;
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
        /// O código não traz e-mail nem senha padrão: uma senha escrita aqui seria pública
        /// junto com o repositório. Quem instala informa as duas em Seed:EmailAdministrador
        /// e Seed:SenhaAdministrador (ADMIN_EMAIL e ADMIN_SENHA no .env do Docker).
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

            var email = configuracao["Seed:EmailAdministrador"]?.Trim().ToLowerInvariant();
            var senha = configuracao["Seed:SenhaAdministrador"];

            // Subir sem administrador deixaria o sistema no ar e inacessível; parar aqui
            // põe no log o que falta configurar.
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
            {
                throw new InvalidOperationException(
                    "Não há administrador ativo no banco e a conta inicial não foi configurada. Defina ADMIN_EMAIL e " +
                    "ADMIN_SENHA no arquivo .env (Docker) ou Seed:EmailAdministrador e Seed:SenhaAdministrador " +
                    "(dotnet user-secrets) e reinicie a API.");
            }

            if (!new EmailAddressAttribute().IsValid(email))
            {
                throw new InvalidOperationException($"ADMIN_EMAIL (Seed:EmailAdministrador) não é um e-mail válido: {email}.");
            }

            if (PasswordHasher.ValidarForca(senha) is { } erroDaSenha)
            {
                throw new InvalidOperationException($"ADMIN_SENHA (Seed:SenhaAdministrador) foi recusada. {erroDaSenha}");
            }

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

            // A senha nunca vai para o log: só quem a definiu a conhece.
            logger.LogWarning(
                "Conta de administrador criada para {Email} com a senha definida em Seed:SenhaAdministrador. " +
                "Altere a senha no primeiro acesso e apague ADMIN_SENHA do .env.",
                email);
        }
    }
}
