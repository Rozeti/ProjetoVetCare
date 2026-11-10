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
    /// <summary>HU-004 e HU-006: cadastro das sessões (início e fim, uma ou várias), conflito e cancelamento.</summary>
    public class AgendaTests : BaseDeTeste
    {
        private CadastrarSessoesUseCase CriarCasoDeUso(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new ClinicaRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new BloqueioAgendaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private static HorarioDeSessaoDTO Horario(DateTime inicio, int minutos = 60) =>
            new() { Inicio = inicio, Fim = inicio.AddMinutes(minutos) };

        private CadastrarSessoesDTO Agendamento(DateTime quando, int minutos = 60) => Cadastro(Horario(quando, minutos));

        private CadastrarSessoesDTO Cadastro(params HorarioDeSessaoDTO[] horarios) => new()
        {
            TratamentoId = Tratamento.Id,
            VeterinarioId = Veterinario.Id,
            Sessoes = horarios.ToList()
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
            resultado.Dados!.Should().ContainSingle();
            resultado.Dados[0].Status.Should().Be("Aguardando confirmação");
            resultado.Dados[0].Situacao.Should().Be(SituacaoSessao.Agendada);
        }

        [Fact]
        public async Task Horario_de_fim_e_obrigatorio()
        {
            var dto = Cadastro(new HorarioDeSessaoDTO { Inicio = ProximoHorarioValido(), Fim = null });

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Validacao);
            resultado.Mensagem.Should().Contain("término");
            Contexto.Sessoes.Should().BeEmpty();
        }

        [Fact]
        public async Task Fim_antes_do_inicio_e_recusado()
        {
            var inicio = ProximoHorarioValido();

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Executar(Cadastro(new HorarioDeSessaoDTO { Inicio = inicio, Fim = inicio.AddMinutes(-30) }));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("depois do início");
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
        public async Task Varias_sessoes_sao_cadastradas_de_uma_vez_com_um_unico_aviso_ao_tutor()
        {
            var primeira = ProximoHorarioValido();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Cadastro(
                Horario(primeira), Horario(primeira.AddDays(7)), Horario(primeira.AddDays(14), minutos: 45)));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Mensagem.Should().Be("3 sessões cadastradas.");
            resultado.Dados!.Select(s => s.DataHoraFim - s.DataHora)
                .Should().Equal(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromMinutes(45));

            Contexto.Sessoes.Count(s => s.TratamentoId == Tratamento.Id).Should().Be(3);
            Contexto.Notificacoes.Count(n => n.UsuarioId == UsuarioTutor.Id).Should().Be(1, "o cadastro em lote gera um só aviso");
        }

        [Fact]
        public async Task Mais_sessoes_podem_ser_cadastradas_depois_no_mesmo_tratamento()
        {
            var inicio = ProximoHorarioValido();

            (await CriarCasoDeUso(ComoAdministrador()).Executar(Cadastro(Horario(inicio), Horario(inicio.AddDays(7)))))
                .Sucesso.Should().BeTrue();

            var depois = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(inicio.AddDays(14)));

            depois.Sucesso.Should().BeTrue(depois.Mensagem);
            Contexto.Sessoes.Count(s => s.TratamentoId == Tratamento.Id).Should().Be(3);
        }

        [Fact]
        public async Task Sessoes_do_mesmo_cadastro_nao_podem_se_sobrepor()
        {
            var inicio = ProximoHorarioValido();

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Executar(Cadastro(Horario(inicio), Horario(inicio.AddMinutes(30))));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("se sobrepõem");
            Contexto.Sessoes.Should().BeEmpty();
        }

        [Fact]
        public async Task Cadastro_com_uma_sessao_em_conflito_nao_grava_nenhuma()
        {
            var ocupado = ProximoHorarioValido();
            await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(ocupado));

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Executar(Cadastro(Horario(ocupado.AddDays(7)), Horario(ocupado.AddMinutes(30))));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            resultado.Mensagem.Should().StartWith("Sessão de ");
            Contexto.Sessoes.Count().Should().Be(1, "o cadastro é tudo ou nada");
        }

        [Fact]
        public async Task Duas_sessoes_no_mesmo_horario_violam_a_exclusividade()
        {
            var horario = ProximoHorarioValido();

            (await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario))).Sucesso.Should().BeTrue();

            var segunda = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario));

            segunda.Sucesso.Should().BeFalse();
            segunda.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Sessao_dentro_da_duracao_de_outra_tambem_conflita()
        {
            var horario = ProximoHorarioValido();

            await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario));

            // A primeira vai das 14h às 15h, então uma sessão às 14h30 ainda colide.
            var sobreposta = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario.AddMinutes(30)));

            sobreposta.Sucesso.Should().BeFalse();
            sobreposta.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task O_fim_informado_define_o_horario_ocupado()
        {
            var horario = ProximoHorarioValido();

            // Das 14h às 14h30: a sessão seguinte pode começar às 14h30 em ponto.
            await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario, minutos: 30));

            var emSeguida = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario.AddMinutes(30), minutos: 30));
            var sobreposta = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario.AddMinutes(15), minutos: 30));

            emSeguida.Sucesso.Should().BeTrue(emSeguida.Mensagem);
            sobreposta.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Sessao_cancelada_libera_o_horario()
        {
            var horario = ProximoHorarioValido();

            var primeira = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario));

            var sessao = await Contexto.Sessoes.FindAsync(primeira.Dados![0].Id);
            sessao!.Status = "Cancelada";
            await Contexto.SaveChangesAsync();

            var segunda = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(horario));

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
            foreach (var status in new[] { StatusTratamento.Concluido, StatusTratamento.Inativado })
            {
                Tratamento.Status = status;
                await Contexto.SaveChangesAsync();

                var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(ProximoHorarioValido()));

                resultado.Sucesso.Should().BeFalse($"um tratamento {status} não recebe sessões");
                resultado.Mensagem.Should().Contain("concluído ou inativado");
            }
        }

        [Fact]
        public async Task Tratamento_planejado_aceita_sessoes()
        {
            Tratamento.Status = StatusTratamento.Planejado;
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Agendamento(ProximoHorarioValido()));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
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

        [Fact]
        public async Task Colega_agenda_na_propria_agenda_uma_sessao_para_paciente_de_outro_veterinario()
        {
            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Veterinária que cobre a ausência",
                Email = "cobertura@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };

            var outroVeterinario = new Veterinario { UsuarioId = outroUsuario.Id, Crmv = "CRMV-DF 2100" };

            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Veterinarios.Add(outroVeterinario);
            await Contexto.SaveChangesAsync();

            var comoOutro = ComoUsuario(outroUsuario, Clinica.Id, veterinarioId: outroVeterinario.Id);
            var agendamento = Agendamento(ProximoHorarioValido());
            agendamento.VeterinarioId = outroVeterinario.Id;

            var resultado = await CriarCasoDeUso(comoOutro).Executar(agendamento);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados![0].VeterinarioId.Should().Be(outroVeterinario.Id);
        }
    }
}
