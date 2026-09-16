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
    /// <summary>HU-007, HU-008 e RN-004: registros clínicos, validações e rastreabilidade.</summary>
    public class RegistrosClinicosTests : BaseDeTeste
    {
        private RegistrarAvaliacaoUseCase CriarCasoDeAvaliacao(UsuarioAtual usuarioAtual) => new(
            new AvaliacaoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new ObservacaoInternaRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            usuarioAtual);

        private RegistrarAtendimentoUseCase CriarCasoDeAtendimento(UsuarioAtual usuarioAtual) => new(
            new AtendimentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new PetRepository(Contexto),
            new MidiaRepository(Contexto),
            new ObservacaoInternaRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            usuarioAtual);

        private CriarAvaliacaoDTO AvaliacaoCompleta() => new()
        {
            TratamentoId = Tratamento.Id,
            VeterinarioId = Veterinario.Id,
            QueixaPrincipal = "Dificuldade para levantar",
            Anamnese = "Quadro progressivo",
            ExameFisico = "Dor à extensão do quadril",
            HipoteseDiagnostica = "Displasia coxofemoral",
            PlanoTerapeutico = "Hidroterapia semanal"
        };

        private async Task<Sessao> CriarSessao()
        {
            var sessao = new Sessao
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                DataHora = DateTime.UtcNow.AddDays(1),
                Status = "Confirmada"
            };

            Contexto.Sessoes.Add(sessao);
            await Contexto.SaveChangesAsync();

            return sessao;
        }

        [Fact]
        public async Task Avaliacao_com_todos_os_campos_e_registrada()
        {
            var resultado = await CriarCasoDeAvaliacao(ComoVeterinario()).Executar(AvaliacaoCompleta());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.QueixaPrincipal.Should().Be("Dificuldade para levantar");
        }

        [Theory]
        [InlineData("QueixaPrincipal", "queixa principal")]
        [InlineData("Anamnese", "anamnese")]
        [InlineData("ExameFisico", "exame físico")]
        [InlineData("HipoteseDiagnostica", "hipótese diagnóstica")]
        [InlineData("PlanoTerapeutico", "plano terapêutico")]
        public async Task Avaliacao_sem_campo_obrigatorio_aponta_qual_falta(string campo, string nomeEsperado)
        {
            var dto = AvaliacaoCompleta();
            typeof(CriarAvaliacaoDTO).GetProperty(campo)!.SetValue(dto, string.Empty);

            var resultado = await CriarCasoDeAvaliacao(ComoVeterinario()).Executar(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain(nomeEsperado);
        }

        [Fact]
        public async Task Correcao_da_avaliacao_arquiva_a_versao_anterior()
        {
            var casoDeUso = CriarCasoDeAvaliacao(ComoVeterinario());
            var criada = await casoDeUso.Executar(AvaliacaoCompleta());

            var atualizacao = await casoDeUso.Atualizar(criada.Dados!.Id, new AtualizarAvaliacaoDTO
            {
                QueixaPrincipal = "Dificuldade para levantar após repouso",
                Anamnese = "Quadro progressivo",
                ExameFisico = "Dor à extensão do quadril",
                HipoteseDiagnostica = "Displasia coxofemoral",
                PlanoTerapeutico = "Hidroterapia duas vezes por semana"
            });

            atualizacao.Sucesso.Should().BeTrue(atualizacao.Mensagem);
            atualizacao.Dados!.DataUltimaEdicao.Should().NotBeNull();

            var historico = await casoDeUso.ObterHistorico(criada.Dados.Id);

            historico.Dados.Should().ContainSingle();
            historico.Dados![0].ConteudoAnterior.Should().Contain("Dificuldade para levantar");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(11)]
        [InlineData(100)]
        public async Task Escala_de_dor_fora_do_intervalo_e_recusada(int escalaDor)
        {
            var sessao = await CriarSessao();

            var resultado = await CriarCasoDeAtendimento(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                VeterinarioId = Veterinario.Id,
                TecnicasAplicadas = "Hidroterapia",
                EscalaDor = escalaDor,
                EvolucaoClinica = "Sem alterações"
            });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("0 e 10");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task Escala_de_dor_nos_limites_do_intervalo_e_aceita(int escalaDor)
        {
            var sessao = await CriarSessao();

            var resultado = await CriarCasoDeAtendimento(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                VeterinarioId = Veterinario.Id,
                TecnicasAplicadas = "Hidroterapia",
                EscalaDor = escalaDor,
                EvolucaoClinica = "Registro de teste"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.EscalaDor.Should().Be(escalaDor);
        }

        [Fact]
        public async Task Uma_sessao_gera_no_maximo_um_atendimento()
        {
            var sessao = await CriarSessao();
            var casoDeUso = CriarCasoDeAtendimento(ComoVeterinario());

            var dto = new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                VeterinarioId = Veterinario.Id,
                TecnicasAplicadas = "Hidroterapia",
                EscalaDor = 4,
                EvolucaoClinica = "Primeira gravação"
            };

            (await casoDeUso.Executar(dto)).Sucesso.Should().BeTrue();

            var segunda = await casoDeUso.Executar(dto);

            segunda.Sucesso.Should().BeFalse();
            segunda.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Atendimento_registra_o_peso_e_alimenta_o_cadastro_do_paciente()
        {
            var sessao = await CriarSessao();

            await CriarCasoDeAtendimento(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                VeterinarioId = Veterinario.Id,
                TecnicasAplicadas = "Cinesioterapia",
                EscalaDor = 3,
                EvolucaoClinica = "Boa resposta",
                PesoKg = 18.4m
            });

            var pet = await Contexto.Pets.FindAsync(Paciente.Id);

            pet!.PesoAtualKg.Should().Be(18.4m);
        }

        [Fact]
        public async Task Atendimento_conclui_a_sessao_automaticamente()
        {
            var sessao = await CriarSessao();

            await CriarCasoDeAtendimento(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                VeterinarioId = Veterinario.Id,
                TecnicasAplicadas = "Laserterapia",
                EscalaDor = 2,
                EvolucaoClinica = "Evolução favorável",
                ConcluirSessao = true
            });

            var atualizada = await Contexto.Sessoes.FindAsync(sessao.Id);

            atualizada!.Status.Should().Be("Concluída");
        }

        [Fact]
        public async Task Sessao_cancelada_nao_aceita_atendimento()
        {
            var sessao = await CriarSessao();
            sessao.Status = "Cancelada";
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeAtendimento(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                VeterinarioId = Veterinario.Id,
                TecnicasAplicadas = "Hidroterapia",
                EscalaDor = 3,
                EvolucaoClinica = "Teste"
            });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("cancelada");
        }
    }
}
