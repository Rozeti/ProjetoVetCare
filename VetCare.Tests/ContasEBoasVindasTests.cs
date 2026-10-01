using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services.Email;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// Ciclo de vida da conta: quem é cadastrado pela clínica recebe por e-mail o caminho
    /// para a primeira senha, e a senha provisória gerada pelo Administrador chega ao
    /// próprio usuário (HU-002, CA-5).
    /// </summary>
    public class ContasEBoasVindasTests : BaseDeTeste
    {
        private readonly FilaDeEmails _fila = new();
        private readonly PasswordHasher _hasher = new();

        private GerenciarTutoresUseCase CriarCasoDeTutores(UsuarioAtual usuarioAtual) => new(
            new TutorRepository(Contexto),
            new UsuarioRepository(Contexto),
            _hasher,
            Dependencias.Contas(Contexto, _fila),
            usuarioAtual);

        private GerenciarUsuariosUseCase CriarCasoDeUsuarios(UsuarioAtual usuarioAtual) => new(
            new UsuarioRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new TutorRepository(Contexto),
            new ApoioRepository(Contexto),
            _hasher,
            Dependencias.Contas(Contexto, _fila),
            new ContasAtivas(new MemoryCache(new MemoryCacheOptions())),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private RecuperarSenhaUseCase CriarCasoDeSenha() => new(
            new UsuarioRepository(Contexto),
            new TokenRedefinicaoRepository(Contexto),
            _hasher,
            Dependencias.Auditoria(Contexto, ComoAdministrador()),
            Dependencias.Contas(Contexto, _fila));

        private List<MensagemDeEmail> EsvaziarFila()
        {
            var lista = new List<MensagemDeEmail>();

            while (_fila.TentarRetirar(out var mensagem))
            {
                lista.Add(mensagem!);
            }

            return lista;
        }

        [Fact]
        public async Task Tutor_cadastrado_sem_senha_cria_a_propria_pelo_email_de_boas_vindas()
        {
            var cadastro = await CriarCasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Ana Souza",
                Email = "ana@teste.com",
                Telefone = "(61) 99999-0000"
            });

            cadastro.Sucesso.Should().BeTrue(cadastro.Mensagem);
            cadastro.Mensagem.Should().Contain("criar a própria senha");

            var email = EsvaziarFila().Should().ContainSingle().Subject;
            email.Destinatario.Should().Be("ana@teste.com");
            email.CorpoHtml.Should().Contain("/primeiro-acesso?token=");

            var pedido = Contexto.TokensRedefinicaoSenha.Single(t => t.UsuarioId == cadastro.Dados!.UsuarioId);
            pedido.Finalidade.Should().Be(FinalidadesDoToken.PrimeiroAcesso);
            pedido.ExpiraEm.Should().BeAfter(DateTime.UtcNow.AddHours(24), "a pessoa pode abrir o e-mail dias depois");

            // O código do e-mail define a primeira senha, e ela passa a valer.
            var codigo = ExtrairCodigo(email.CorpoTexto);

            var definicao = await CriarCasoDeSenha().Redefinir(new RedefinirSenhaDTO
            {
                Email = "ana@teste.com", Codigo = codigo, NovaSenha = "MinhaSenha1"
            });

            definicao.Sucesso.Should().BeTrue(definicao.Mensagem);
            definicao.Mensagem.Should().Contain("criada");

            var usuario = Contexto.Usuarios.Single(u => u.Email == "ana@teste.com");
            _hasher.Verificar("MinhaSenha1", usuario.SenhaHash, out _).Should().BeTrue();
        }

        [Fact]
        public async Task Tutor_cadastrado_com_senha_recebe_boas_vindas_sem_link_de_senha()
        {
            var cadastro = await CriarCasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Bruno Lima",
                Email = "bruno@teste.com",
                Senha = "SenhaDaClinica1"
            });

            cadastro.Sucesso.Should().BeTrue(cadastro.Mensagem);

            var email = EsvaziarFila().Should().ContainSingle().Subject;
            email.CorpoHtml.Should().NotContain("primeiro-acesso");
            email.CorpoTexto.Should().Contain("senha inicial foi informada pela equipe");

            Contexto.TokensRedefinicaoSenha.Should().BeEmpty();
        }

        [Fact]
        public async Task Senha_curta_e_recusada_no_cadastro_do_tutor()
        {
            var cadastro = await CriarCasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Carla", Email = "carla@teste.com", Senha = "123"
            });

            cadastro.Sucesso.Should().BeFalse();
            cadastro.Mensagem.Should().Contain("mínimo");
            _fila.Pendentes.Should().Be(0);
        }

        [Fact]
        public async Task Tutor_so_enxerga_o_proprio_cadastro()
        {
            var outro = await CriarCasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Outro Tutor", Email = "outro@teste.com", Senha = "Senha123"
            });

            var leitura = await CriarCasoDeTutores(ComoTutor()).ObterPorId(outro.Dados!.Id);

            leitura.Sucesso.Should().BeFalse("o CPF e o telefone de outro tutor não podem ser lidos");

            var proprio = await CriarCasoDeTutores(ComoTutor()).ObterPorId(Tutor.Id);
            proprio.Sucesso.Should().BeTrue();
        }

        [Fact]
        public async Task Administrador_redefine_a_senha_e_o_usuario_recebe_a_provisoria_por_email()
        {
            var resultado = await CriarCasoDeUsuarios(ComoAdministrador()).RedefinirSenha(UsuarioTutor.Id);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.SenhaProvisoria.Should().HaveLength(10);

            var email = EsvaziarFila().Should().ContainSingle().Subject;
            email.Destinatario.Should().Be(UsuarioTutor.Email);
            email.CorpoTexto.Should().Contain(resultado.Dados.SenhaProvisoria);

            var usuario = await Contexto.Usuarios.FindAsync(UsuarioTutor.Id);
            _hasher.Verificar(resultado.Dados.SenhaProvisoria, usuario!.SenhaHash, out _).Should().BeTrue();
        }

        [Fact]
        public async Task Usuario_cadastrado_pelo_administrador_sem_senha_recebe_link_de_primeiro_acesso()
        {
            var resultado = await CriarCasoDeUsuarios(ComoAdministrador()).Cadastrar(new CriarUsuarioDTO
            {
                Nome = "Recepção Nova",
                Email = "recepcao@teste.com",
                Perfil = Perfis.Apoio,
                Setor = "Recepção"
            }, Clinica.Id);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var email = EsvaziarFila().Should().ContainSingle().Subject;
            email.CorpoHtml.Should().Contain("/primeiro-acesso?token=");
        }

        [Fact]
        public async Task Preferencias_de_notificacao_sao_gravadas_na_conta()
        {
            var resultado = await CriarCasoDeUsuarios(ComoTutor()).AtualizarPreferenciasDeNotificacao(
                new PreferenciasDeNotificacaoDTO { NotificarPorEmail = false, NotificarPorPush = true });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.NotificarPorEmail.Should().BeFalse();
            resultado.Dados.NotificarPorPush.Should().BeTrue();

            var usuario = await Contexto.Usuarios.FindAsync(UsuarioTutor.Id);
            usuario!.NotificarPorEmail.Should().BeFalse();
        }

        [Fact]
        public async Task Trocar_a_propria_senha_envia_aviso_de_seguranca()
        {
            UsuarioTutor.SenhaHash = _hasher.Gerar("Atual123");
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUsuarios(ComoTutor()).AlterarPropriaSenha(
                new AlterarSenhaDTO { SenhaAtual = "Atual123", NovaSenha = "Nova12345" });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var email = EsvaziarFila().Should().ContainSingle().Subject;
            email.Assunto.Should().Contain("Sua senha foi alterada");
        }

        private static string ExtrairCodigo(string corpoTexto)
        {
            var linha = corpoTexto.Split('\n').First(l => l.StartsWith("Código:"));
            return linha["Código:".Length..].Trim();
        }
    }
}
