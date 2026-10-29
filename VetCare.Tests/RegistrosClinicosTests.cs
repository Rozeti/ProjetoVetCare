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
    /// <summary>HU-007, HU-008 e RN-004: avaliação, evolução, validações e rastreabilidade.</summary>
    public class RegistrosClinicosTests : BaseDeTeste
    {
        private RegistrarAvaliacaoUseCase CriarCasoDeAvaliacao(UsuarioAtual usuarioAtual) => new(
            new AvaliacaoRepository(Contexto),
            new PetRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new ObservacaoInternaRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private RegistrarAtendimentoUseCase CriarCasoDeEvolucao(UsuarioAtual usuarioAtual) => new(
            new AtendimentoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new PetRepository(Contexto),
            new MidiaRepository(Contexto),
            new ObservacaoInternaRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Assinador(),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private CriarAvaliacaoDTO AvaliacaoCompleta() => new()
        {
            PacienteId = Paciente.Id,
            VeterinarioId = Veterinario.Id,
            MotivoConsulta = "Dificuldade para levantar",
            Observacoes = "Quadro progressivo; dor à extensão do quadril.",
            DiagnosticoPossivel = "Displasia coxofemoral"
        };

        private CriarAtendimentoDTO Evolucao(Guid sessaoId, int escalaDor = 3, string reacao = "Tolerou bem os exercícios") => new()
        {
            SessaoId = sessaoId,
            VeterinarioId = Veterinario.Id,
            TecnicasAplicadas = "Hidroterapia",
            EscalaDor = escalaDor,
            EvolucaoClinica = "Esteira aquática por 15 minutos",
            ReacaoAnimal = reacao
        };

        private async Task<Sessao> CriarSessao(Guid? tratamentoId = null, int diasAFrente = 1)
        {
            var sessao = new Sessao
            {
                TratamentoId = tratamentoId ?? Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                DataHora = DateTime.UtcNow.AddDays(diasAFrente),
                Status = StatusSessao.Confirmada
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
            resultado.Dados!.MotivoConsulta.Should().Be("Dificuldade para levantar");
            resultado.Dados.NomeVeterinario.Should().Be("Dra. Veterinária");
        }

        [Fact]
        public async Task Avaliacao_nao_depende_de_tratamento()
        {
            // A avaliação é registrada para o animal; nenhum tratamento é exigido nem criado.
            var resultado = await CriarCasoDeAvaliacao(ComoVeterinario()).Executar(AvaliacaoCompleta());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Tratamentos.Should().BeEmpty();
        }

        [Theory]
        [InlineData("MotivoConsulta", "motivo da consulta")]
        [InlineData("Observacoes", "observações")]
        [InlineData("DiagnosticoPossivel", "possível diagnóstico")]
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
                MotivoConsulta = "Dificuldade para levantar após repouso",
                Observacoes = "Quadro progressivo; dor à extensão do quadril.",
                DiagnosticoPossivel = "Displasia coxofemoral"
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

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(Evolucao(sessao.Id, escalaDor));

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

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(Evolucao(sessao.Id, escalaDor));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.EscalaDor.Should().Be(escalaDor);
        }

        [Fact]
        public async Task Evolucao_exige_a_reacao_do_animal()
        {
            var sessao = await CriarSessao();

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(Evolucao(sessao.Id, reacao: "  "));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("como o animal reagiu");
        }

        [Fact]
        public async Task Evolucao_registra_reacao_observacoes_e_relatorio()
        {
            var sessao = await CriarSessao();
            var dto = Evolucao(sessao.Id);
            dto.Observacoes = "Manter a mesma carga na próxima sessão.";
            dto.Relatorio = "Ganho de amplitude de 10 graus desde o início.";

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(dto);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.ReacaoAnimal.Should().Be("Tolerou bem os exercícios");
            resultado.Dados.Observacoes.Should().Be("Manter a mesma carga na próxima sessão.");
            resultado.Dados.Relatorio.Should().Be("Ganho de amplitude de 10 graus desde o início.");
            resultado.Dados.NomeTratamento.Should().Be("Reabilitação de teste");
        }

        [Fact]
        public async Task Evolucoes_sao_numeradas_por_tratamento()
        {
            var casoDeUso = CriarCasoDeEvolucao(ComoVeterinario());

            var primeira = await casoDeUso.Executar(Evolucao((await CriarSessao(diasAFrente: 1)).Id));
            var segunda = await casoDeUso.Executar(Evolucao((await CriarSessao(diasAFrente: 2)).Id));
            var terceira = await casoDeUso.Executar(Evolucao((await CriarSessao(diasAFrente: 3)).Id));

            new[] { primeira, segunda, terceira }.Select(r => r.Dados!.NumeroSessao).Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task Primeira_evolucao_poe_o_tratamento_planejado_em_andamento()
        {
            Tratamento.Status = StatusTratamento.Planejado;
            await Contexto.SaveChangesAsync();

            var sessao = await CriarSessao();

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(Evolucao(sessao.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            (await Contexto.Tratamentos.FindAsync(Tratamento.Id))!.Status.Should().Be(StatusTratamento.EmAndamento);
        }

        [Fact]
        public async Task Tratamento_inativado_nao_recebe_novas_evolucoes()
        {
            var sessao = await CriarSessao();

            Tratamento.Status = StatusTratamento.Inativado;
            Tratamento.DataFim = DateTime.UtcNow;
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(Evolucao(sessao.Id));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            resultado.Mensagem.Should().Contain("Reative-o");
        }

        [Fact]
        public async Task Uma_sessao_gera_no_maximo_uma_evolucao()
        {
            var sessao = await CriarSessao();
            var casoDeUso = CriarCasoDeEvolucao(ComoVeterinario());

            (await casoDeUso.Executar(Evolucao(sessao.Id))).Sucesso.Should().BeTrue();

            var segunda = await casoDeUso.Executar(Evolucao(sessao.Id));

            segunda.Sucesso.Should().BeFalse();
            segunda.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Evolucao_registra_o_peso_e_alimenta_o_cadastro_do_paciente()
        {
            var sessao = await CriarSessao();
            var dto = Evolucao(sessao.Id);
            dto.PesoKg = 18.4m;

            await CriarCasoDeEvolucao(ComoVeterinario()).Executar(dto);

            var pet = await Contexto.Pets.FindAsync(Paciente.Id);

            pet!.PesoAtualKg.Should().Be(18.4m);
        }

        [Fact]
        public async Task Evolucao_conclui_a_sessao_automaticamente()
        {
            var sessao = await CriarSessao();
            var dto = Evolucao(sessao.Id);
            dto.ConcluirSessao = true;

            await CriarCasoDeEvolucao(ComoVeterinario()).Executar(dto);

            var atualizada = await Contexto.Sessoes.FindAsync(sessao.Id);

            atualizada!.Status.Should().Be(StatusSessao.Concluida);
        }

        [Fact]
        public async Task Sessao_cancelada_nao_aceita_evolucao()
        {
            var sessao = await CriarSessao();
            sessao.Status = StatusSessao.Cancelada;
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeEvolucao(ComoVeterinario()).Executar(Evolucao(sessao.Id));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("cancelada");
        }

        [Fact]
        public async Task Correcao_da_evolucao_arquiva_a_versao_anterior()
        {
            var sessao = await CriarSessao();
            var casoDeUso = CriarCasoDeEvolucao(ComoVeterinario());
            var criada = await casoDeUso.Executar(Evolucao(sessao.Id));

            var correcao = await casoDeUso.Atualizar(criada.Dados!.Id, new AtualizarAtendimentoDTO
            {
                TecnicasAplicadas = "Hidroterapia, Laserterapia",
                EscalaDor = 2,
                EvolucaoClinica = "Esteira aquática por 20 minutos",
                ReacaoAnimal = "Ficou agitado no início e depois relaxou"
            });

            correcao.Sucesso.Should().BeTrue(correcao.Mensagem);
            correcao.Dados!.NumeroSessao.Should().Be(1, "a correção não muda a posição da sessão no tratamento");

            var historico = await casoDeUso.ObterHistorico(criada.Dados.Id);

            historico.Dados.Should().ContainSingle();
            historico.Dados![0].ConteudoAnterior.Should().Contain("\"ReacaoAnimal\":\"Tolerou bem os exerc");
        }
    }
}
