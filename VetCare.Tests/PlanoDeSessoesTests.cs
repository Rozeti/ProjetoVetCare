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
    /// <summary>
    /// HU-004: o plano de sessões de um tratamento — a quantidade, os dias da semana e o horário
    /// viram datas (prévia), e as sessões além do previsto ampliam o plano do tratamento.
    /// </summary>
    public class PlanoDeSessoesTests : BaseDeTeste
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

        private static RelogioDaClinica Relogio => RelogioDaClinica.Padrao;

        /// <summary>A próxima segunda-feira depois de amanhã, no calendário da clínica.</summary>
        private static DateTime ProximaSegunda()
        {
            var dia = Relogio.Hoje.AddDays(2);

            while (dia.DayOfWeek != DayOfWeek.Monday)
            {
                dia = dia.AddDays(1);
            }

            return dia;
        }

        private PlanoDeSessoesDTO Plano(int quantidade, DateTime aPartirDe, params DayOfWeek[] dias) => new()
        {
            TratamentoId = Tratamento.Id,
            VeterinarioId = Veterinario.Id,
            Quantidade = quantidade,
            APartirDe = aPartirDe,
            HoraInicio = "09:00",
            HoraFim = "10:00",
            DiasDaSemana = dias.Select(d => (int)d).ToList()
        };

        private static List<DateTime> Locais(PreviaDeSessoesDTO previa) =>
            previa.Sessoes.Select(s => Relogio.ParaLocal(s.Inicio!.Value)).ToList();

        [Fact]
        public async Task Previa_monta_as_datas_nos_dias_da_semana_escolhidos_sem_gravar()
        {
            var segunda = ProximaSegunda();

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Previa(Plano(4, segunda, DayOfWeek.Monday, DayOfWeek.Wednesday));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            Locais(resultado.Dados!).Should().Equal(
                segunda.AddHours(9), segunda.AddDays(2).AddHours(9), segunda.AddDays(7).AddHours(9), segunda.AddDays(9).AddHours(9));
            resultado.Dados!.Sessoes.Should().OnlyContain(s => s.Fim - s.Inicio == TimeSpan.FromHours(1));
            resultado.Dados.Puladas.Should().BeEmpty();
            resultado.Dados.NovoTotalPrevisto.Should().Be(16, "4 sessões cabem nas 16 previstas");
            Contexto.Sessoes.Should().BeEmpty("a prévia não grava nada");
        }

        [Fact]
        public async Task Previa_pula_dias_bloqueados_ou_ocupados_e_mantem_a_quantidade()
        {
            var segunda = ProximaSegunda();
            var quarta = segunda.AddDays(2);

            Contexto.BloqueiosAgenda.Add(new BloqueioAgenda
            {
                VeterinarioId = Veterinario.Id,
                Inicio = Relogio.ParaUtc(segunda),
                Fim = Relogio.ParaUtc(segunda.AddDays(1)),
                Motivo = "Congresso",
                CriadoPorId = UsuarioAdministrador.Id
            });
            Contexto.Sessoes.Add(new Sessao
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                DataHora = Relogio.ParaUtc(quarta.AddHours(9.5)),
                DataHoraFim = Relogio.ParaUtc(quarta.AddHours(10.5)),
                Status = StatusSessao.AguardandoConfirmacao
            });
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoAdministrador())
                .Previa(Plano(2, segunda, DayOfWeek.Monday, DayOfWeek.Wednesday));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            Locais(resultado.Dados!).Should().Equal(segunda.AddDays(7).AddHours(9), segunda.AddDays(9).AddHours(9));
            resultado.Dados!.Puladas.Select(p => p.Data.Date).Should().Equal(segunda, quarta);
            resultado.Dados.Puladas[0].Motivo.Should().Contain("Congresso");
            resultado.Dados.Puladas[1].Motivo.Should().Contain("já tem uma sessão");
        }

        [Fact]
        public async Task Previa_recusa_horario_fora_do_expediente()
        {
            var plano = Plano(2, ProximaSegunda(), DayOfWeek.Monday);
            plano.HoraInicio = "06:00";
            plano.HoraFim = "07:00";

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Previa(plano);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("A clínica atende");
        }

        [Fact]
        public async Task Previa_exige_dia_da_semana_e_termino_depois_do_inicio()
        {
            var semDias = await CriarCasoDeUso(ComoAdministrador()).Previa(Plano(2, ProximaSegunda()));
            semDias.Mensagem.Should().Contain("dia da semana");

            var plano = Plano(2, ProximaSegunda(), DayOfWeek.Monday);
            plano.HoraFim = "08:30";
            var invertido = await CriarCasoDeUso(ComoAdministrador()).Previa(plano);
            invertido.Mensagem.Should().Contain("depois do início");
        }

        [Fact]
        public async Task As_datas_da_previa_sao_cadastradas_de_uma_vez()
        {
            var previa = await CriarCasoDeUso(ComoAdministrador())
                .Previa(Plano(3, ProximaSegunda(), DayOfWeek.Tuesday, DayOfWeek.Thursday));

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(new CadastrarSessoesDTO
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                Sessoes = previa.Dados!.Sessoes
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Mensagem.Should().Be("3 sessões cadastradas.");
            Contexto.Tratamentos.Single(t => t.Id == Tratamento.Id).SessoesPrevistas.Should().Be(16);
        }

        [Fact]
        public async Task Sessoes_alem_do_previsto_ampliam_o_plano_do_tratamento()
        {
            Contexto.Tratamentos.Single(t => t.Id == Tratamento.Id).SessoesPrevistas = 2;
            await Contexto.SaveChangesAsync();

            var casoDeUso = CriarCasoDeUso(ComoAdministrador());
            var previa = await casoDeUso.Previa(Plano(3, ProximaSegunda(), DayOfWeek.Monday));

            previa.Dados!.SessoesPrevistas.Should().Be(2);
            previa.Dados.NovoTotalPrevisto.Should().Be(3, "a prévia avisa que o plano vai crescer");

            var resultado = await casoDeUso.Executar(new CadastrarSessoesDTO
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                Sessoes = previa.Dados.Sessoes
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Mensagem.Should().Contain("passa a prever 3 sessões");
            Contexto.Tratamentos.Single(t => t.Id == Tratamento.Id).SessoesPrevistas.Should().Be(3);
            Contexto.RegistrosAuditoria.Should().Contain(r => r.Entidade == "Tratamento" && r.Detalhe.Contains("ampliado de 2 para 3"));
        }

        [Fact]
        public async Task Previa_nao_vale_para_tratamento_encerrado()
        {
            Contexto.Tratamentos.Single(t => t.Id == Tratamento.Id).Status = StatusTratamento.Concluido;
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Previa(Plano(2, ProximaSegunda(), DayOfWeek.Monday));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("concluído ou inativado");
        }
    }
}
