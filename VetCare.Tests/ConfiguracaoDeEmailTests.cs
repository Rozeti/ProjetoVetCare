using System.Net.Sockets;
using FluentAssertions;
using MailKit.Security;
using VetCare.API.Services.Email;

namespace VetCare.Tests
{
    /// <summary>
    /// Para o e-mail sair de verdade basta preencher servidor, usuário e senha: o remetente
    /// segue a conta autenticada quando ninguém o definiu, e as falhas do provedor chegam
    /// ao log em português, dizendo o que corrigir.
    /// </summary>
    public class ConfiguracaoDeEmailTests
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
    }
}
