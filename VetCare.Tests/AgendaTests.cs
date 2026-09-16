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
    /// <summary>HU-004 e HU-006: regras de agendamento, conflito e cancelamento.</summary>
    public class AgendaTests : BaseDeTeste
    {
        private AgendarSessaoUseCase CriarCasoDeUso(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new ClinicaRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new BloqueioAgendaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            usuarioAtual);

        private AgendarSessaoDTO Agendamento(DateTime quando) => new()
        {
            TratamentoId = Tratamento.Id,
            VeterinarioId = Veterinario.Id,
            DataHora = quando
        };

        /// <summary>Próximo dia útil às 14h locais, dentro do expediente da clínica.</summary>
        private static DateTime ProximoHorarioValido(int diasAFrente = 3)
        {
            var data = DateTime.Now.Date.AddDays(diasAFrente);

            while (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                data = data.AddDays(1);
            }

            return DateTime.SpecifyKind(data.AddHours(14), DateTimeKind.Local).ToUniversalTime();
        }

        [Fact]
        public async Task Sessao_nova_nasce_aguardando_confirmacao()
        {
            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(ProximoHorarioValido()));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Status.Should().Be("Aguardando confirmação");
        }

        [Fact]
        public async Task Agendamento_retroativo_e_bloqueado()
        {
            var ontem = DateTime.UtcNow.AddDays(-1);

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(ontem));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Validacao);
            resultado.Mensagem.Should().Contain("passado");
        }

        [Fact]
        public async Task Duas_sessoes_no_mesmo_horario_violam_a_exclusividade()
        {
            var horario = ProximoHorarioValido();
            var casoDeUso = CriarCasoDeUso(ComoAdministrador());

            (await casoDeUso.Executar(Agendamento(horario))).Sucesso.Should().BeTrue();

            var segunda = await casoDeUso.Executar(Agendamento(horario));

            segunda.Sucesso.Should().BeFalse();
            segunda.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Sessao_dentro_da_duracao_de_outra_tambem_conflita()
        {
            var horario = ProximoHorarioValido();
            var casoDeUso = CriarCasoDeUso(ComoAdministrador());

            await casoDeUso.Executar(Agendamento(horario));

            // A sessão padrão dura 60 minutos, então 30 minutos depois ainda colide.
            var sobreposta = await casoDeUso.Executar(Agendamento(horario.AddMinutes(30)));

            sobreposta.Sucesso.Should().BeFalse();
            sobreposta.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Sessao_cancelada_libera_o_horario()
        {
            var horario = ProximoHorarioValido();
            var casoDeUso = CriarCasoDeUso(ComoAdministrador());

            var primeira = await casoDeUso.Executar(Agendamento(horario));

            var sessao = await Contexto.Sessoes.FindAsync(primeira.Dados!.Id);
            sessao!.Status = "Cancelada";
            await Contexto.SaveChangesAsync();

            var segunda = await casoDeUso.Executar(Agendamento(horario));

            segunda.Sucesso.Should().BeTrue(segunda.Mensagem);
        }

        [Fact]
        public async Task Horario_fora_do_expediente_e_recusado()
        {
            var madrugada = DateTime.SpecifyKind(
                DateTime.Now.Date.AddDays(3).AddHours(3), DateTimeKind.Local).ToUniversalTime();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(madrugada));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("atende das");
        }

        [Fact]
        public async Task Periodo_bloqueado_impede_o_agendamento()
        {
            var horario = ProximoHorarioValido();

            Contexto.BloqueiosAgenda.Add(new BloqueioAgenda
            {
                VeterinarioId = Veterinario.Id,
                Inicio = horario.AddHours(-1),
                Fim = horario.AddHours(2),
                Motivo = "Congresso",
                CriadoPorId = UsuarioAdministrador.Id
            });

            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            resultado.Mensagem.Should().Contain("Congresso");
        }

        [Fact]
        public async Task Tratamento_encerrado_nao_aceita_novas_sessoes()
        {
            Tratamento.Status = "Concluído";
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(ProximoHorarioValido()));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("em andamento");
        }

        [Fact]
        public async Task Veterinario_nao_agenda_na_agenda_de_outro_profissional()
        {
            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Outro Veterinário",
                Email = "outro@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };

            var outroVeterinario = new Veterinario
            {
                UsuarioId = outroUsuario.Id,
                Crmv = "CRMV-DF 2000"
            };

            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Veterinarios.Add(outroVeterinario);
            await Contexto.SaveChangesAsync();

            var comoOutro = ComoUsuario(outroUsuario, Clinica.Id, veterinarioId: outroVeterinario.Id);

            var resultado = await CriarCasoDeUso(comoOutro).Executar(Agendamento(ProximoHorarioValido()));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }
    }
}
