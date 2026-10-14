using FluentAssertions;
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
    /// "Esqueci minha senha": o pedido gera um link (portal) e um código (aplicativo),
    /// ambos de uso único e com prazo, enviados ao e-mail cadastrado. O que precisa
    /// valer: ninguém descobre quais e-mails existem, o código resiste a tentativa e
    /// erro, e a senha nova funciona de fato.
    /// </summary>
    public class RecuperacaoDeSenhaTests : BaseDeTeste
    {
        private readonly FilaDeEmails _fila = new();
        private readonly PasswordHasher _hasher = new();

        private RecuperarSenhaUseCase CriarCasoDeUso(string ambiente = "Development") => new(
            new UsuarioRepository(Contexto),
            new TokenRedefinicaoRepository(Contexto),
            _hasher,
            Dependencias.Auditoria(Contexto, ComoAdministrador()),
            Dependencias.Contas(Contexto, _fila, ambiente));

        private MensagemDeEmail UltimoEmail()
        {
            MensagemDeEmail? ultimo = null;

            while (_fila.TentarRetirar(out var mensagem))
            {
                ultimo = mensagem;
            }

            ultimo.Should().NotBeNull("um e-mail deveria ter sido enfileirado");
            return ultimo!;
        }

        [Fact]
        public async Task Email_desconhecido_recebe_resposta_neutra_e_nenhum_email_sai()
        {
            var resultado = await CriarCasoDeUso().Solicitar(new SolicitarRecuperacaoDTO { Email = "ninguem@teste.com" });

            resultado.Sucesso.Should().BeTrue();
            resultado.Dados!.Mensagem.Should().Contain("Se houver uma conta");
            resultado.Dados.TokenDesenvolvimento.Should().BeNull();
            _fila.Pendentes.Should().Be(0);
        }

        [Fact]
        public async Task Pedido_envia_email_com_link_e_codigo_ao_endereco_cadastrado()
        {
            var resultado = await CriarCasoDeUso().Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            resultado.Sucesso.Should().BeTrue();

            var email = UltimoEmail();
            email.Destinatario.Should().Be(UsuarioTutor.Email);
            email.Assunto.Should().Contain("Redefinição de senha");
            email.CorpoHtml.Should().Contain("/recuperar-senha?token=" + resultado.Dados!.TokenDesenvolvimento);
            email.CorpoTexto.Should().Contain(resultado.Dados.CodigoDesenvolvimento);
            resultado.Dados.CodigoDesenvolvimento.Should().HaveLength(6).And.MatchRegex("^[0-9]{6}$");
        }

        [Fact]
        public async Task Fora_de_desenvolvimento_as_credenciais_nao_voltam_na_resposta()
        {
            var resultado = await CriarCasoDeUso("Production").Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            resultado.Dados!.TokenDesenvolvimento.Should().BeNull();
            resultado.Dados.CodigoDesenvolvimento.Should().BeNull();

            Contexto.TokensRedefinicaoSenha.Should().ContainSingle(t => t.UsuarioId == UsuarioTutor.Id,
                "o pedido existe mesmo que só o e-mail o entregue");
            _fila.Pendentes.Should().Be(1);
        }

        [Fact]
        public async Task Conta_inativa_nao_recebe_email()
        {
            UsuarioTutor.Ativo = false;
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso().Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            resultado.Sucesso.Should().BeTrue("a resposta é a mesma para não revelar o estado da conta");
            _fila.Pendentes.Should().Be(0);
        }

        [Fact]
        public async Task Token_do_link_redefine_a_senha_e_avisa_por_email()
        {
            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            var resultado = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Token = pedido.Dados!.TokenDesenvolvimento,
                NovaSenha = "NovaSenha2026"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var usuario = await Contexto.Usuarios.FindAsync(UsuarioTutor.Id);
            _hasher.Verificar("NovaSenha2026", usuario!.SenhaHash, out _).Should().BeTrue();

            var aviso = UltimoEmail();
            aviso.Assunto.Should().Contain("Sua senha foi alterada");
        }

        [Fact]
        public async Task Email_e_codigo_redefinem_a_senha_pelo_aplicativo()
        {
            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            var resultado = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Email = UsuarioTutor.Email,
                Codigo = pedido.Dados!.CodigoDesenvolvimento,
                NovaSenha = "OutraSenha2026"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var usuario = await Contexto.Usuarios.FindAsync(UsuarioTutor.Id);
            _hasher.Verificar("OutraSenha2026", usuario!.SenhaHash, out _).Should().BeTrue();
        }

        [Fact]
        public async Task Codigo_aceita_espacos_e_tracos_copiados_do_email()
        {
            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });
            var codigo = pedido.Dados!.CodigoDesenvolvimento!;

            var resultado = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Email = UsuarioTutor.Email,
                Codigo = $"{codigo[..3]} {codigo[3..]}",
                NovaSenha = "SenhaComEspaco"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
        }

        [Fact]
        public async Task Codigo_errado_consome_tentativas_e_depois_descarta_o_pedido()
        {
            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });
            var codigoCerto = pedido.Dados!.CodigoDesenvolvimento!;
            var codigoErrado = codigoCerto == "000000" ? "111111" : "000000";

            for (var tentativa = 1; tentativa < TokenRedefinicaoSenha.MaximoTentativasDeCodigo; tentativa++)
            {
                var erro = await casoDeUso.Redefinir(new RedefinirSenhaDTO
                {
                    Email = UsuarioTutor.Email, Codigo = codigoErrado, NovaSenha = "Qualquer1"
                });

                erro.Sucesso.Should().BeFalse();
                erro.Mensagem.Should().Contain("tentativa");
            }

            var ultimaErrada = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Email = UsuarioTutor.Email, Codigo = codigoErrado, NovaSenha = "Qualquer1"
            });

            ultimaErrada.Mensagem.Should().Contain("esgotou");

            // Mesmo o código certo deixa de valer: o pedido foi descartado.
            var comCodigoCerto = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Email = UsuarioTutor.Email, Codigo = codigoCerto, NovaSenha = "Qualquer1"
            });

            comCodigoCerto.Sucesso.Should().BeFalse();
        }

        [Fact]
        public async Task Token_e_de_uso_unico()
        {
            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });
            var token = pedido.Dados!.TokenDesenvolvimento;

            (await casoDeUso.Redefinir(new RedefinirSenhaDTO { Token = token, NovaSenha = "Primeira1" })).Sucesso.Should().BeTrue();

            var segunda = await casoDeUso.Redefinir(new RedefinirSenhaDTO { Token = token, NovaSenha = "Segunda1" });

            segunda.Sucesso.Should().BeFalse();
            segunda.Mensagem.Should().Contain("inválido ou expirado");
        }

        [Fact]
        public async Task Novo_pedido_invalida_o_link_anterior()
        {
            var casoDeUso = CriarCasoDeUso();
            var primeiro = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });
            var segundo = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            var comOAntigo = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Token = primeiro.Dados!.TokenDesenvolvimento, NovaSenha = "Antiga123"
            });

            comOAntigo.Sucesso.Should().BeFalse();

            var comONovo = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Token = segundo.Dados!.TokenDesenvolvimento, NovaSenha = "Nova12345"
            });

            comONovo.Sucesso.Should().BeTrue(comONovo.Mensagem);
        }

        [Fact]
        public async Task Token_expirado_e_recusado()
        {
            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            var registro = Contexto.TokensRedefinicaoSenha.Single(t => t.UsuarioId == UsuarioTutor.Id);
            registro.ExpiraEm = DateTime.UtcNow.AddMinutes(-1);
            await Contexto.SaveChangesAsync();

            var resultado = await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Token = pedido.Dados!.TokenDesenvolvimento, NovaSenha = "Atrasada1"
            });

            resultado.Sucesso.Should().BeFalse();
        }

        [Fact]
        public async Task Redefinicao_libera_a_conta_bloqueada_por_tentativas()
        {
            UsuarioTutor.TentativasFalhas = 4;
            UsuarioTutor.BloqueadoAte = DateTime.UtcNow.AddMinutes(10);
            await Contexto.SaveChangesAsync();

            var casoDeUso = CriarCasoDeUso();
            var pedido = await casoDeUso.Solicitar(new SolicitarRecuperacaoDTO { Email = UsuarioTutor.Email });

            await casoDeUso.Redefinir(new RedefinirSenhaDTO
            {
                Token = pedido.Dados!.TokenDesenvolvimento, NovaSenha = "Liberada1"
            });

            var usuario = await Contexto.Usuarios.FindAsync(UsuarioTutor.Id);
            usuario!.BloqueadoAte.Should().BeNull();
            usuario.TentativasFalhas.Should().Be(0);
        }

        [Fact]
        public async Task Sem_token_nem_codigo_o_pedido_e_recusado()
        {
            var resultado = await CriarCasoDeUso().Redefinir(new RedefinirSenhaDTO { NovaSenha = "Qualquer1" });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("link");
        }
    }
}
