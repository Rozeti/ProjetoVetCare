using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using VetCare.API.Controllers;
using VetCare.API.Security;

namespace VetCare.Tests
{
    /// <summary>
    /// RN-014: a ficha cadastral de tutores e pacientes é mantida pelo Administrador e pelo
    /// Assistente Administrativo. Ao Veterinário cabe a consulta e o registro clínico.
    ///
    /// A autorização dessas rotas é declarada por atributo, fora do alcance dos testes de caso
    /// de uso. Estes testes leem os atributos para que uma reabertura acidental do acesso
    /// apareça aqui, e não em produção.
    /// </summary>
    public class PermissoesDeCadastroTests
    {
        private static string PapeisDe<T>(string metodo)
        {
            var alvo = typeof(T).GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance);
            alvo.Should().NotBeNull($"o método {metodo} precisa existir em {typeof(T).Name}");

            var autorizacao = alvo!.GetCustomAttribute<AuthorizeAttribute>();
            autorizacao.Should().NotBeNull($"{metodo} precisa declarar os perfis permitidos");

            return autorizacao!.Roles ?? string.Empty;
        }

        [Theory]
        [InlineData(nameof(PetsController.Cadastrar))]
        [InlineData(nameof(PetsController.Atualizar))]
        [InlineData(nameof(PetsController.AlterarStatus))]
        public void Cadastro_de_paciente_e_restrito_a_administrador_e_apoio(string metodo)
        {
            var papeis = PapeisDe<PetsController>(metodo);

            papeis.Should().Be(Perfis.AdministradorOuApoio);
            papeis.Should().NotContain(Perfis.Veterinario);
        }

        [Fact]
        public void Cadastro_de_tutor_e_restrito_a_administrador_e_apoio()
        {
            var papeis = PapeisDe<TutoresController>(nameof(TutoresController.Cadastrar));

            papeis.Should().Be(Perfis.AdministradorOuApoio);
            papeis.Should().NotContain(Perfis.Veterinario);
        }

        [Theory]
        [InlineData(nameof(UsuariosController.Cadastrar))]
        [InlineData(nameof(UsuariosController.Atualizar))]
        public void Cadastro_de_usuario_interno_e_exclusivo_do_administrador(string metodo)
        {
            PapeisDe<UsuariosController>(metodo).Should().Be(Perfis.Administrador);
        }

        /// <summary>
        /// A consulta continua aberta à equipe: o veterinário precisa alcançar qualquer
        /// paciente e qualquer tutor para atender o animal.
        /// </summary>
        [Fact]
        public void Consulta_de_pacientes_e_tutores_continua_aberta_a_equipe()
        {
            PapeisDe<PetsController>(nameof(PetsController.ListarParaSelecao))
                .Should().Be(Perfis.EquipeClinica);

            PapeisDe<TutoresController>(nameof(TutoresController.Listar))
                .Should().Be(Perfis.EquipeClinica);

            PapeisDe<TutoresController>(nameof(TutoresController.ListarParaSelecao))
                .Should().Be(Perfis.EquipeClinica);
        }
    }
}
