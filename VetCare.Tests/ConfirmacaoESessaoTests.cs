using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>HU-006 e RN-009: confirmação, cancelamento e o prazo mínimo de antecedência.</summary>
    public class ConfirmacaoESessaoTests : BaseDeTeste
    {
        private AtualizarStatusSessaoUseCase CriarCasoDeUso(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new ClinicaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            usuarioAtual);

        private async Task<Sessao> CriarSessao(DateTime quando, string status = "Aguardando confirmação")
        {
            var sessao = new Sessao
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                DataHora = quando,
                Status = status
            };

            Contexto.Sessoes.Add(sessao);
            await Contexto.SaveChangesAsync();

            return sessao;
        }

        [Fact]
        public async Task Tutor_confirma_a_presenca()
        {
            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(5));

            var resultado = await CriarCasoDeUso(ComoTutor())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Confirmada" });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var atualizada = await Contexto.Sessoes.FindAsync(sessao.Id);
            atualizada!.Status.Should().Be("Confirmada");
        }

        [Fact]
        public async Task Cancelamento_dentro_do_prazo_e_aceito()
        {
            // A clínica exige 12 horas de antecedência por padrão.
            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(3));

            var resultado = await CriarCasoDeUso(ComoTutor())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Cancelada" });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
        }

        [Fact]
        public async Task Cancelamento_fora_do_prazo_e_sinalizado()
        {
            var sessao = await CriarSessao(DateTime.UtcNow.AddHours(2));

            var resultado = await CriarCasoDeUso(ComoTutor())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Cancelada" });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("antecedência");
        }

        [Fact]
        public async Task Prazo_de_cancelamento_segue_a_configuracao_da_clinica()
        {
            Clinica.HorasMinimasCancelamento = 48;
            await Contexto.SaveChangesAsync();

            // Vinte e quatro horas bastariam no prazo padrão, mas não no configurado.
            var sessao = await CriarSessao(DateTime.UtcNow.AddHours(24));

            var resultado = await CriarCasoDeUso(ComoTutor())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Cancelada" });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("48");
        }

        [Fact]
        public async Task Tutor_nao_conclui_sessao()
        {
            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(2));

            var resultado = await CriarCasoDeUso(ComoTutor())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Concluída" });

            resultado.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }

        [Fact]
        public async Task Sessao_concluida_nao_muda_mais_de_status()
        {
            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(-1), "Concluída");

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Confirmada" });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("concluída");
        }

        [Fact]
        public async Task Status_invalido_e_recusado()
        {
            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(2));

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Remarcada" });

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Validacao);
        }

        [Fact]
        public async Task Tutor_nao_altera_sessao_de_pet_de_outro_tutor()
        {
            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Outro Tutor",
                Email = "outro@teste.com",
                Perfil = Perfis.Tutor,
                SenhaHash = "hash"
            };

            var outroTutor = new Tutor { UsuarioId = outroUsuario.Id };

            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Tutores.Add(outroTutor);
            await Contexto.SaveChangesAsync();

            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(5));
            var comoOutro = ComoUsuario(outroUsuario, Clinica.Id, tutorId: outroTutor.Id);

            var resultado = await CriarCasoDeUso(comoOutro)
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Confirmada" });

            resultado.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }

        [Fact]
        public async Task Confirmacao_pelo_tutor_notifica_o_veterinario()
        {
            var sessao = await CriarSessao(DateTime.UtcNow.AddDays(5));

            await CriarCasoDeUso(ComoTutor())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = "Confirmada" });

            var notificacoes = Contexto.Notificacoes
                .Where(n => n.UsuarioId == UsuarioVeterinario.Id)
                .ToList();

            notificacoes.Should().ContainSingle();
        }
    }
}
