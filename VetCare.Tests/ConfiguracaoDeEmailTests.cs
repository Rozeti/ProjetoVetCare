using System.Net.Sockets;
using FluentAssertions;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VetCare.API.Data;
using VetCare.API.Services.Email;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// Para o e-mail sair de verdade basta preencher servidor, usuário e senha: o remetente
    /// segue a conta autenticada quando ninguém o definiu, as falhas do provedor chegam em
    /// português e o administrador consegue testar o canal pela tela de configurações.
    /// </summary>
    public class ConfiguracaoDeEmailTests : BaseDeTeste
    {
        [Fact]
        public void Sem_smtp_o_remetente_de_fabrica_e_mantido()
        {
            var opcoes = new OpcoesDeEmail();

            opcoes.SmtpConfigurado.Should().BeFalse();
            opcoes.RemetenteEfetivo.Should().Be(OpcoesDeEmail.RemetentePadrao);
        }

        [Fact]
        public void Com_smtp_e_remetente_de_fabrica_o_usuario_do_smtp_assina_os_emails()
        {
            var opcoes = new OpcoesDeEmail
            {
                Smtp = { Host = "smtp.gmail.com", Usuario = "clinica@gmail.com", Senha = "senha-de-app" }
            };

            opcoes.RemetenteEfetivo.Should().Be("clinica@gmail.com");
        }

        [Fact]
        public void Remetente_em_branco_tambem_cai_no_usuario_do_smtp()
        {
            var opcoes = new OpcoesDeEmail
            {
                Remetente = "   ",
                Smtp = { Host = "smtp-relay.brevo.com", Usuario = "contato@clinica.com.br" }
            };

            opcoes.RemetenteEfetivo.Should().Be("contato@clinica.com.br");
        }

        [Fact]
        public void Remetente_definido_pela_clinica_prevalece_sobre_o_usuario_do_smtp()
        {
            var opcoes = new OpcoesDeEmail
            {
                Remetente = "nao-responda@clinica.com.br",
                Smtp = { Host = "smtp.sendgrid.net", Usuario = "apikey" }
            };

            opcoes.RemetenteEfetivo.Should().Be("nao-responda@clinica.com.br");
        }

        [Fact]
        public void Usuario_do_smtp_que_nao_e_email_nao_vira_remetente()
        {
            var opcoes = new OpcoesDeEmail
            {
                Smtp = { Host = "smtp.sendgrid.net", Usuario = "apikey" }
            };

            opcoes.RemetenteEfetivo.Should().Be(OpcoesDeEmail.RemetentePadrao,
                "um remetente inventado é melhor do que 'apikey', que nem endereço é");
        }

        [Fact]
        public void Falha_de_autenticacao_vira_orientacao_sobre_senha_de_app()
        {
            var explicacao = FalhasDeEmail.Descrever(new AuthenticationException("535-5.7.8 Username and Password not accepted"));

            explicacao.Should().Contain("senha de app").And.Contain("usuário ou a senha");
        }

        [Fact]
        public void Falha_de_conexao_aponta_para_host_e_porta_mesmo_embrulhada_em_outra_excecao()
        {
            var excecao = new IOException("fechado", new SocketException((int)SocketError.ConnectionRefused));

            FalhasDeEmail.Descrever(excecao).Should().Contain("SMTP_HOST").And.Contain("SMTP_PORTA");
        }

        [Fact]
        public void Erro_desconhecido_preserva_a_mensagem_original()
        {
            FalhasDeEmail.Descrever(new InvalidOperationException("algo inesperado"))
                .Should().Contain("algo inesperado");
        }

        [Fact]
        public async Task Envio_de_teste_vai_para_o_email_do_proprio_administrador()
        {
            var email = new EmailDeTeste();
            var diagnostico = CriarDiagnostico(email, new OpcoesDeEmail
            {
                Smtp = { Host = "smtp.gmail.com", Usuario = "clinica@gmail.com", Senha = "x" }
            });

            var resultado = await diagnostico.EnviarTeste(CancellationToken.None);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Destinatario.Should().Be(UsuarioAdministrador.Email);
            resultado.Dados.Canal.Should().Be("smtp");
            resultado.Dados.Mensagem.Should().Contain(UsuarioAdministrador.Email);

            email.Enviados.Should().ContainSingle();
            email.Enviados[0].Destinatario.Should().Be(UsuarioAdministrador.Email);
            email.Enviados[0].Assunto.Should().Contain("E-mail de teste");
            // O HTML codifica os acentos (est&#225;); a versão em texto é a legível.
            email.Enviados[0].CorpoTexto.Should().Contain("está funcionando");
        }

        [Fact]
        public async Task Envio_de_teste_sem_servidor_avisa_que_o_email_ficou_na_pasta_local()
        {
            var diagnostico = CriarDiagnostico(new EmailDeTeste { EnviaDeVerdade = false }, new OpcoesDeEmail());

            var resultado = await diagnostico.EnviarTeste(CancellationToken.None);

            resultado.Sucesso.Should().BeTrue();
            resultado.Dados!.Canal.Should().Be("local");
            resultado.Dados.Mensagem.Should().Contain("emails-enviados");
        }

        [Fact]
        public async Task Recusa_do_servidor_volta_como_erro_de_validacao_explicado()
        {
            var email = new EmailDeTeste { FalharCom = "SMTP fora do ar" };
            var diagnostico = CriarDiagnostico(email, new OpcoesDeEmail { Smtp = { Host = "smtp.exemplo.com" } });

            var resultado = await diagnostico.EnviarTeste(CancellationToken.None);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("SMTP fora do ar");
        }

        [Fact]
        public void Situacao_descreve_o_canal_e_o_remetente_efetivo()
        {
            var diagnostico = CriarDiagnostico(new EmailDeTeste(), new OpcoesDeEmail
            {
                Smtp = { Host = "smtp.gmail.com", Porta = 587, Usuario = "clinica@gmail.com" }
            });

            var situacao = diagnostico.Situacao();

            situacao.Canal.Should().Be("smtp");
            situacao.EnviaDeVerdade.Should().BeTrue();
            situacao.Servidor.Should().Be("smtp.gmail.com:587");
            situacao.Remetente.Should().Be("clinica@gmail.com");
        }

        private DiagnosticoDeEmail CriarDiagnostico(EmailDeTeste email, OpcoesDeEmail opcoes) =>
            new(email,
                Dependencias.ModelosDeEmail(),
                Options.Create(opcoes),
                new UsuarioRepository(Contexto),
                ComoAdministrador(),
                NullLogger<DiagnosticoDeEmail>.Instance);
    }
}
