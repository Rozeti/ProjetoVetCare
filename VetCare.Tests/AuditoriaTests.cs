using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// A tela de auditoria se concentra no que mudou nos dados: criação, alteração, inativação
    /// e exclusão. Logins, consultas e downloads continuam gravados, mas não aparecem nela.
    /// </summary>
    public class AuditoriaTests : BaseDeTeste
    {
        private ConsultarAuditoriaUseCase CasoDeUso() => new(new AuditoriaRepository(Contexto), ComoAdministrador());

        private RegistroAuditoria Registro(string acao, string entidade, string detalhe) => new()
        {
            ClinicaId = Clinica.Id,
            UsuarioId = UsuarioAdministrador.Id,
            NomeUsuario = UsuarioAdministrador.Nome,
            Perfil = Perfis.Administrador,
            Acao = acao,
            Entidade = entidade,
            Detalhe = detalhe,
            DataHora = DateTime.UtcNow
        };

        private void GravarRegistrosDeTodosOsTipos()
        {
            Contexto.RegistrosAuditoria.AddRange(
                Registro("Login", "Autenticacao", "Entrou no sistema"),
                Registro("LoginNegado", "Autenticacao", "Senha errada"),
                Registro("Consulta", "Prontuario", "Abriu o prontuário de Thor"),
                Registro("Download", "DocumentoClinico", "Baixou o laudo"),
                Registro("RedefinicaoSenha", "Usuario", "Senha provisória gerada"),
                Registro("Criacao", "Paciente", "Cadastro de Pipoca"),
                Registro("Alteracao", "Paciente", "Edição de Pipoca"),
                Registro("Inativacao", "Paciente", "Inativação de Pipoca"),
                Registro("Exclusao", "Vacina", "Exclusão da V10 lançada por engano"));
            Contexto.SaveChanges();
        }

        [Fact]
        public async Task Trilha_mostra_so_criacoes_alteracoes_inativacoes_e_exclusoes()
        {
            GravarRegistrosDeTodosOsTipos();

            var resultado = await CasoDeUso().Listar(null, null, null, null, null, new ParametrosPagina());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Itens.Select(r => r.Acao).Should()
                .BeEquivalentTo(new[] { "Criacao", "Alteracao", "Inativacao", "Exclusao" });

            // Os demais registros continuam no banco, só não aparecem na tela.
            Contexto.RegistrosAuditoria.Should().Contain(r => r.Acao == "Login");
        }

        [Fact]
        public async Task Filtrar_por_login_ou_consulta_nao_traz_nada()
        {
            GravarRegistrosDeTodosOsTipos();

            var logins = await CasoDeUso().Listar(null, "Login", null, null, null, new ParametrosPagina());
            var consultas = await CasoDeUso().Listar(null, "Consulta", null, null, null, new ParametrosPagina());
            var criacoes = await CasoDeUso().Listar(null, "Criacao", null, null, null, new ParametrosPagina());

            logins.Dados!.Itens.Should().BeEmpty();
            consultas.Dados!.Itens.Should().BeEmpty();
            criacoes.Dados!.Itens.Select(r => r.Detalhe).Should().Equal("Cadastro de Pipoca");
        }
    }
}
