using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.Tests.Suporte
{
    /// <summary>
    /// Base dos testes de regra de negócio. Cada teste recebe um banco em memória
    /// isolado e um cenário mínimo já montado (clínica, veterinário, tutor e paciente),
    /// para que cada caso exercite apenas a regra que pretende verificar.
    /// </summary>
    public abstract class BaseDeTeste : IDisposable
    {
        protected AppDbContext Contexto { get; }
        protected Clinica Clinica { get; }
        protected Usuario UsuarioAdministrador { get; }
        protected Usuario UsuarioVeterinario { get; }
        protected Usuario UsuarioTutor { get; }
        protected Veterinario Veterinario { get; }
        protected Tutor Tutor { get; }
        protected Pet Paciente { get; }
        protected Prontuario Prontuario { get; }

        /// <summary>Avaliação de origem do tratamento do cenário.</summary>
        protected AvaliacaoClinica Avaliacao { get; }

        protected Tratamento Tratamento { get; }

        protected BaseDeTeste()
        {
            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"vetcare-{Guid.NewGuid()}")
                .Options;

            Contexto = new AppDbContext(opcoes);

            Clinica = new Clinica { Nome = "Clínica de Teste" };

            UsuarioAdministrador = CriarUsuario("Administradora", "admin@teste.com", Perfis.Administrador);
            UsuarioVeterinario = CriarUsuario("Dra. Veterinária", "vet@teste.com", Perfis.Veterinario);
            UsuarioTutor = CriarUsuario("Tutor Responsável", "tutor@teste.com", Perfis.Tutor);

            Veterinario = new Veterinario
            {
                UsuarioId = UsuarioVeterinario.Id,
                Crmv = "CRMV-DF 1000",
                Especialidade = "Fisioterapia"
            };

            Tutor = new Tutor { UsuarioId = UsuarioTutor.Id, Telefone = "(61) 90000-0000" };

            Paciente = new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = Tutor.Id,
                // O veterinário do cenário é quem acompanha o paciente; é ele quem o enxerga.
                VeterinarioResponsavelId = Veterinario.Id,
                Nome = "Paciente de Teste",
                Especie = "Cachorro",
                Raca = "SRD",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2020, 1, 10), DateTimeKind.Utc)
            };

            Prontuario = new Prontuario { PacienteId = Paciente.Id };

            // Todo tratamento nasce de uma avaliação: o cenário traz as duas.
            Avaliacao = new AvaliacaoClinica
            {
                PacienteId = Paciente.Id,
                ProntuarioId = Prontuario.Id,
                VeterinarioId = Veterinario.Id,
                MotivoConsulta = "Claudicação do membro posterior",
                Observacoes = "Dor à palpação do joelho direito.",
                DiagnosticoPossivel = "Ruptura parcial do ligamento cruzado",
                DataRegistro = DateTime.UtcNow.AddDays(-11)
            };

            Tratamento = new Tratamento
            {
                PacienteId = Paciente.Id,
                AvaliacaoId = Avaliacao.Id,
                VeterinarioId = Veterinario.Id,
                DataInicio = DateTime.UtcNow.AddDays(-10),
                Nome = "Reabilitação de teste",
                Tipo = "Fisioterapia",
                Descricao = "Reabilitação de teste",
                DuracaoPrevista = "2 meses",
                Frequencia = "2 sessões por semana",
                SessoesPrevistas = 16,
                Status = StatusTratamento.EmAndamento
            };

            Contexto.Clinicas.Add(Clinica);
            Contexto.Usuarios.AddRange(UsuarioAdministrador, UsuarioVeterinario, UsuarioTutor);
            Contexto.Veterinarios.Add(Veterinario);
            Contexto.Tutores.Add(Tutor);
            Contexto.Pets.Add(Paciente);
            Contexto.Prontuarios.Add(Prontuario);
            Contexto.AvaliacoesClinicas.Add(Avaliacao);
            Contexto.Tratamentos.Add(Tratamento);
            Contexto.SaveChanges();
        }

        private Usuario CriarUsuario(string nome, string email, string perfil) => new()
        {
            ClinicaId = Clinica.Id,
            Nome = nome,
            Email = email,
            Perfil = perfil,
            SenhaHash = "hash",
            Ativo = true
        };

        /// <summary>
        /// Monta um <see cref="UsuarioAtual"/> com as claims que o JWT carregaria, que é
        /// a forma como os casos de uso identificam quem está agindo.
        /// </summary>
        protected static UsuarioAtual ComoUsuario(
            Usuario usuario,
            Guid clinicaId,
            Guid? veterinarioId = null,
            Guid? tutorId = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Name, usuario.Nome),
                new(ClaimTypes.Email, usuario.Email),
                new(ClaimTypes.Role, usuario.Perfil),
                new(ClaimsPersonalizadas.ClinicaId, clinicaId.ToString())
            };

            if (veterinarioId.HasValue)
            {
                claims.Add(new Claim(ClaimsPersonalizadas.VeterinarioId, veterinarioId.Value.ToString()));
            }

            if (tutorId.HasValue)
            {
                claims.Add(new Claim(ClaimsPersonalizadas.TutorId, tutorId.Value.ToString()));
            }

            var contexto = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"))
            };

            return new UsuarioAtual(new HttpContextAccessor { HttpContext = contexto });
        }

        protected UsuarioAtual ComoAdministrador() => ComoUsuario(UsuarioAdministrador, Clinica.Id);

        protected UsuarioAtual ComoVeterinario() =>
            ComoUsuario(UsuarioVeterinario, Clinica.Id, veterinarioId: Veterinario.Id);

        protected UsuarioAtual ComoTutor() =>
            ComoUsuario(UsuarioTutor, Clinica.Id, tutorId: Tutor.Id);

        public void Dispose()
        {
            Contexto.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
