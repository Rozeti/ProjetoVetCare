using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// HU-015: cada notificação gravada no sistema chega ao e-mail e ao celular do
    /// usuário. A tabela de notificações é a fila: o registro só fica "Entregue" quando os
    /// canais aplicáveis foram atendidos, e uma falha passageira vira nova tentativa.
    /// </summary>
    public class EntregaDeNotificacoesTests : BaseDeTeste
    {
        private const string TokenDoCelular = "ExponentPushToken[abcdefghijklmnopqrstuv]";

        private readonly EmailDeTeste _email = new();
        private readonly PushDeTeste _push = new();

        private EntregadorDeNotificacoes CriarEntregador()
        {
            var servicos = new ServiceCollection();
            servicos.AddScoped<INotificacaoRepository>(_ => new NotificacaoRepository(Contexto));
            servicos.AddScoped<IDispositivoRepository>(_ => new DispositivoRepository(Contexto));

            return new EntregadorDeNotificacoes(
                servicos.BuildServiceProvider(),
                new SinalDeNotificacoes(),
                _email,
                _push,
                Dependencias.ModelosDeEmail(),
                NullLogger<EntregadorDeNotificacoes>.Instance);
        }

        private DispositivosUseCase CriarCasoDeDispositivos(UsuarioAtual usuarioAtual) =>
            new(new DispositivoRepository(Contexto), usuarioAtual);

        private async Task RegistrarCelularDoTutor()
        {
            var resultado = await CriarCasoDeDispositivos(ComoTutor()).Registrar(new RegistrarDispositivoDTO
            {
                TokenPush = TokenDoCelular, Plataforma = "android", NomeDoAparelho = "Celular de teste"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
        }

        private async Task<Notificacao> NotificarTutor()
        {
            await Dependencias.Notificacoes(Contexto)
                .NotificarSessaoAgendada(UsuarioTutor.Id, Paciente.Nome, DateTime.UtcNow.AddDays(2));

            return Contexto.Notificacoes.Single(n => n.UsuarioId == UsuarioTutor.Id);
        }

        [Fact]
        public async Task Notificacao_nova_sai_por_email_e_push_e_fica_entregue()
        {
            await RegistrarCelularDoTutor();
            var notificacao = await NotificarTutor();

            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Pendente);

            var tratadas = await CriarEntregador().EntregarPendentes(CancellationToken.None);

            tratadas.Should().Be(1);

            var email = _email.Enviados.Should().ContainSingle().Subject;
            email.Destinatario.Should().Be(UsuarioTutor.Email);
            email.Assunto.Should().Contain("Nova sessão agendada");
            email.CorpoHtml.Should().Contain("http://portal.teste/minha-agenda");

            var push = _push.Enviadas.Should().ContainSingle().Subject;
            push.TokenPush.Should().Be(TokenDoCelular);
            push.Titulo.Should().Be("Nova sessão agendada");
            push.Dados["link"].Should().Be("/minha-agenda");

            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Entregue);
            notificacao.EmailEnviadoEm.Should().NotBeNull();
            notificacao.PushEnviadoEm.Should().NotBeNull();
        }

        [Fact]
        public async Task Preferencias_desligadas_sao_respeitadas()
        {
            await RegistrarCelularDoTutor();
            UsuarioTutor.NotificarPorEmail = false;
            UsuarioTutor.NotificarPorPush = false;
            await Contexto.SaveChangesAsync();

            var notificacao = await NotificarTutor();
            await CriarEntregador().EntregarPendentes(CancellationToken.None);

            _email.Enviados.Should().BeEmpty();
            _push.Enviadas.Should().BeEmpty();
            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Entregue, "não havia canal a atender");
        }

        [Fact]
        public async Task Sem_celular_registrado_o_email_vai_sozinho()
        {
            var notificacao = await NotificarTutor();
            await CriarEntregador().EntregarPendentes(CancellationToken.None);

            _email.Enviados.Should().ContainSingle();
            _push.Enviadas.Should().BeEmpty();
            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Entregue);
            notificacao.PushEnviadoEm.Should().BeNull();
        }

        [Fact]
        public async Task Falha_no_email_agenda_nova_tentativa_e_desiste_depois_do_limite()
        {
            _email.FalharCom = "SMTP fora do ar";
            var notificacao = await NotificarTutor();
            var entregador = CriarEntregador();

            await entregador.EntregarPendentes(CancellationToken.None);

            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Pendente);
            notificacao.TentativasDeEntrega.Should().Be(1);
            notificacao.ProximaTentativaEm.Should().BeAfter(DateTime.UtcNow);
            notificacao.ErroDeEntrega.Should().Contain("SMTP fora do ar");

            // Enquanto o prazo da nova tentativa não chega, a rodada não a pega.
            (await entregador.EntregarPendentes(CancellationToken.None)).Should().Be(0);

            for (var tentativa = 2; tentativa <= EntregadorDeNotificacoes.MaximoTentativas; tentativa++)
            {
                notificacao.ProximaTentativaEm = null;
                await Contexto.SaveChangesAsync();
                await entregador.EntregarPendentes(CancellationToken.None);
            }

            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Falhou);
            notificacao.TentativasDeEntrega.Should().Be(EntregadorDeNotificacoes.MaximoTentativas);
        }

        [Fact]
        public async Task Email_entregue_nao_e_reenviado_quando_so_o_push_falhou()
        {
            await RegistrarCelularDoTutor();
            _push.FalhaPassageira = true;
            var notificacao = await NotificarTutor();
            var entregador = CriarEntregador();

            await entregador.EntregarPendentes(CancellationToken.None);

            notificacao.EmailEnviadoEm.Should().NotBeNull();
            notificacao.PushEnviadoEm.Should().BeNull();
            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Pendente);

            _push.FalhaPassageira = false;
            notificacao.ProximaTentativaEm = null;
            await Contexto.SaveChangesAsync();

            await entregador.EntregarPendentes(CancellationToken.None);

            _email.Enviados.Should().ContainSingle("o e-mail já tinha saído na primeira rodada");
            _push.Enviadas.Should().HaveCount(2);
            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Entregue);
        }

        [Fact]
        public async Task Token_de_push_morto_desativa_o_aparelho_sem_segurar_a_notificacao()
        {
            await RegistrarCelularDoTutor();
            _push.TokensInvalidos.Add(TokenDoCelular);
            var notificacao = await NotificarTutor();

            await CriarEntregador().EntregarPendentes(CancellationToken.None);

            var aparelho = Contexto.DispositivosDoUsuario.Single(d => d.TokenPush == TokenDoCelular);
            aparelho.Ativo.Should().BeFalse();
            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Entregue);
        }

        [Fact]
        public async Task Conta_desativada_nao_recebe_nada()
        {
            await RegistrarCelularDoTutor();
            UsuarioTutor.Ativo = false;
            await Contexto.SaveChangesAsync();

            var notificacao = await NotificarTutor();
            await CriarEntregador().EntregarPendentes(CancellationToken.None);

            _email.Enviados.Should().BeEmpty();
            _push.Enviadas.Should().BeEmpty();
            notificacao.SituacaoEntrega.Should().Be(SituacoesDeEntrega.Entregue);
        }

        [Fact]
        public async Task Link_da_notificacao_segue_o_perfil_de_quem_recebe()
        {
            var notificacoes = Dependencias.Notificacoes(Contexto);

            await notificacoes.NotificarMudancaStatusSessao(
                UsuarioVeterinario.Id, Paciente.Nome, StatusSessao.Confirmada, DateTime.UtcNow, destinatarioEhTutor: false);

            await notificacoes.NotificarMudancaStatusSessao(
                UsuarioTutor.Id, Paciente.Nome, StatusSessao.Cancelada, DateTime.UtcNow, destinatarioEhTutor: true);

            Contexto.Notificacoes.Single(n => n.UsuarioId == UsuarioVeterinario.Id).LinkRelacionado.Should().Be("/agenda");
            Contexto.Notificacoes.Single(n => n.UsuarioId == UsuarioTutor.Id).LinkRelacionado.Should().Be("/minha-agenda");
        }

        [Fact]
        public async Task Registro_de_aparelho_valida_o_token_e_a_plataforma()
        {
            var casoDeUso = CriarCasoDeDispositivos(ComoTutor());

            (await casoDeUso.Registrar(new RegistrarDispositivoDTO { TokenPush = "qualquer-coisa", Plataforma = "android" }))
                .Sucesso.Should().BeFalse();

            (await casoDeUso.Registrar(new RegistrarDispositivoDTO { TokenPush = TokenDoCelular, Plataforma = "windows" }))
                .Sucesso.Should().BeFalse();
        }

        [Fact]
        public async Task Mesmo_aparelho_troca_de_dono_quando_outra_pessoa_entra_nele()
        {
            await RegistrarCelularDoTutor();

            var comoVeterinario = await CriarCasoDeDispositivos(ComoVeterinario()).Registrar(new RegistrarDispositivoDTO
            {
                TokenPush = TokenDoCelular, Plataforma = "android"
            });

            comoVeterinario.Sucesso.Should().BeTrue();

            var aparelhos = Contexto.DispositivosDoUsuario.Where(d => d.TokenPush == TokenDoCelular).ToList();
            aparelhos.Should().ContainSingle().Which.UsuarioId.Should().Be(UsuarioVeterinario.Id);
        }

        [Fact]
        public async Task Sair_da_conta_desativa_apenas_o_proprio_aparelho()
        {
            await RegistrarCelularDoTutor();

            // Outro usuário tentando "remover" o aparelho do tutor não muda nada.
            await CriarCasoDeDispositivos(ComoVeterinario()).Remover(TokenDoCelular);
            Contexto.DispositivosDoUsuario.Single(d => d.TokenPush == TokenDoCelular).Ativo.Should().BeTrue();

            await CriarCasoDeDispositivos(ComoTutor()).Remover(TokenDoCelular);
            Contexto.DispositivosDoUsuario.Single(d => d.TokenPush == TokenDoCelular).Ativo.Should().BeFalse();
        }
    }
}
