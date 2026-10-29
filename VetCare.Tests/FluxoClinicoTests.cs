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
    /// Fluxo clínico do animal: Animal → Avaliações → Tratamentos vinculados às avaliações →
    /// Evoluções vinculadas aos tratamentos. Receitas e pedidos de exame nascem de uma avaliação;
    /// receitas também de uma evolução.
    /// </summary>
    public class FluxoClinicoTests : BaseDeTeste
    {
        private readonly Usuario _usuarioOutroVeterinario;
        private readonly Veterinario _outroVeterinario;
        private readonly Pet _pacienteDoOutro;

        public FluxoClinicoTests()
        {
            _usuarioOutroVeterinario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Dr. Outro Veterinário",
                Email = "outro.vet@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };

            _outroVeterinario = new Veterinario { UsuarioId = _usuarioOutroVeterinario.Id, Crmv = "CRMV-DF 2000" };

            _pacienteDoOutro = new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = Tutor.Id,
                VeterinarioResponsavelId = _outroVeterinario.Id,
                Nome = "Paciente do Outro",
                Especie = "Gato",
                Raca = "Siamês",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2021, 5, 1), DateTimeKind.Utc)
            };

            Contexto.Usuarios.Add(_usuarioOutroVeterinario);
            Contexto.Veterinarios.Add(_outroVeterinario);
            Contexto.Pets.Add(_pacienteDoOutro);
            Contexto.Prontuarios.Add(new Prontuario { PacienteId = _pacienteDoOutro.Id });
            Contexto.SaveChanges();
        }

        private UsuarioAtual ComoOutroVeterinario() =>
            ComoUsuario(_usuarioOutroVeterinario, Clinica.Id, veterinarioId: _outroVeterinario.Id);

        // ------------------------------------------------------------------ casos de uso

        private RegistrarAvaliacaoUseCase CasoDeAvaliacao(UsuarioAtual usuarioAtual) => new(
            new AvaliacaoRepository(Contexto),
            new PetRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new ObservacaoInternaRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarTratamentosUseCase CasoDeTratamentos(UsuarioAtual usuarioAtual) => new(
            new TratamentoRepository(Contexto),
            new AvaliacaoRepository(Contexto),
            new PetRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new SessaoRepository(Contexto),
            new AtendimentoRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private RegistrarAtendimentoUseCase CasoDeEvolucoes(UsuarioAtual usuarioAtual) => new(
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

        private GerenciarPrescricoesUseCase CasoDeReceitas(UsuarioAtual usuarioAtual) => new(
            new PrescricaoRepository(Contexto),
            new AvaliacaoRepository(Contexto),
            new AtendimentoRepository(Contexto),
            new PetRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new VeterinarioRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarPedidosExameUseCase CasoDeExames(UsuarioAtual usuarioAtual) => new(
            new PedidoExameRepository(Contexto),
            new AvaliacaoRepository(Contexto),
            new PetRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new VeterinarioRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private ConsultarProntuarioUseCase CasoDeProntuario(UsuarioAtual usuarioAtual) => new(
            new ProntuarioRepository(Contexto),
            new PetRepository(Contexto),
            new TratamentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new VacinaRepository(Contexto),
            new PrescricaoRepository(Contexto),
            new PedidoExameRepository(Contexto),
            new AlergiaRepository(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            Dependencias.Assinador(),
            usuarioAtual);

        private GerenciarPacientesUseCase CasoDePacientes(UsuarioAtual usuarioAtual) => new(
            new PetRepository(Contexto),
            new TutorRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new AlergiaRepository(Contexto),
            new VacinaRepository(Contexto),
            new TratamentoRepository(Contexto),
            new VeterinarioRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        // ------------------------------------------------------------------ auxiliares

        private static CriarTratamentoDTO TratamentoDe(Guid avaliacaoId, string nome = "RPG") => new()
        {
            AvaliacaoId = avaliacaoId,
            Nome = nome,
            Tipo = "Fisioterapia",
            Descricao = "Reeducação postural global",
            DuracaoPrevista = "3 meses",
            Frequencia = "1 sessão por semana",
            SessoesPrevistas = 12
        };

        private async Task<Sessao> SessaoDe(Guid tratamentoId, int diasAtras)
        {
            var sessao = new Sessao
            {
                TratamentoId = tratamentoId,
                VeterinarioId = Veterinario.Id,
                DataHora = DateTime.UtcNow.AddDays(-diasAtras),
                Status = StatusSessao.Confirmada
            };

            Contexto.Sessoes.Add(sessao);
            await Contexto.SaveChangesAsync();
            return sessao;
        }

        private async Task<AtendimentoDTO> EvolucaoEm(Guid tratamentoId, int diasAtras, string procedimentos = "Cinesioterapia")
        {
            var sessao = await SessaoDe(tratamentoId, diasAtras);

            var resultado = await CasoDeEvolucoes(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                TecnicasAplicadas = procedimentos,
                EscalaDor = 4,
                EvolucaoClinica = "Exercícios de alongamento",
                ReacaoAnimal = "Colaborativo"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            // A evolução vale para o dia da sessão, não para o momento da digitação.
            var registrada = await Contexto.Atendimentos.FindAsync(resultado.Dados!.Id);
            registrada!.DataRegistro = sessao.DataHora;
            await Contexto.SaveChangesAsync();

            return resultado.Dados;
        }

        // ------------------------------------------------------------------ avaliação e tratamento

        [Fact]
        public async Task Tratamento_nao_existe_sem_avaliacao_de_origem()
        {
            var resultado = await CasoDeTratamentos(ComoVeterinario()).Cadastrar(TratamentoDe(Guid.NewGuid()));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.NaoEncontrado);
            resultado.Mensagem.Should().Contain("Avaliação de origem");
        }

        [Fact]
        public async Task Tratamento_nasce_planejado_e_vinculado_a_avaliacao_e_ao_animal_dela()
        {
            var resultado = await CasoDeTratamentos(ComoVeterinario()).Cadastrar(TratamentoDe(Avaliacao.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Status.Should().Be(StatusTratamento.Planejado);
            resultado.Dados.AvaliacaoId.Should().Be(Avaliacao.Id);
            resultado.Dados.PacienteId.Should().Be(Paciente.Id);
            resultado.Dados.MotivoAvaliacao.Should().Be(Avaliacao.MotivoConsulta);
            resultado.Dados.SessoesPrevistas.Should().Be(12);
        }

        [Theory]
        [InlineData("Nome", "nome")]
        [InlineData("Tipo", "tipo")]
        [InlineData("Descricao", "Descreva")]
        [InlineData("DuracaoPrevista", "duração prevista")]
        [InlineData("Frequencia", "frequência")]
        public async Task Tratamento_exige_os_campos_do_plano(string campo, string trechoDaMensagem)
        {
            var dto = TratamentoDe(Avaliacao.Id);
            typeof(CriarTratamentoDTO).GetProperty(campo)!.SetValue(dto, " ");

            var resultado = await CasoDeTratamentos(ComoVeterinario()).Cadastrar(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain(trechoDaMensagem);
        }

        [Fact]
        public async Task Tratamento_em_andamento_nao_comeca_no_futuro_mas_o_planejado_pode()
        {
            var futuro = DateTime.UtcNow.AddDays(10);

            var emAndamento = TratamentoDe(Avaliacao.Id);
            emAndamento.Status = StatusTratamento.EmAndamento;
            emAndamento.DataInicio = futuro;

            (await CasoDeTratamentos(ComoVeterinario()).Cadastrar(emAndamento)).Sucesso.Should().BeFalse();

            var planejado = TratamentoDe(Avaliacao.Id);
            planejado.DataInicio = futuro;

            (await CasoDeTratamentos(ComoVeterinario()).Cadastrar(planejado)).Sucesso.Should().BeTrue();
        }

        [Fact]
        public async Task Uma_avaliacao_pode_originar_varios_tratamentos_e_outra_nenhum()
        {
            var casoDeUso = CasoDeTratamentos(ComoVeterinario());
            await casoDeUso.Cadastrar(TratamentoDe(Avaliacao.Id, "RPG"));
            await casoDeUso.Cadastrar(TratamentoDe(Avaliacao.Id, "Hidroterapia"));

            var semTratamento = await CasoDeAvaliacao(ComoVeterinario()).Executar(new CriarAvaliacaoDTO
            {
                PacienteId = Paciente.Id,
                MotivoConsulta = "Retorno",
                Observacoes = "Sem queixas novas",
                DiagnosticoPossivel = "Evolução dentro do esperado"
            });

            var prontuario = await CasoDeProntuario(ComoVeterinario()).Executar(Paciente.Id);

            var avaliacoes = prontuario.Dados!.Avaliacoes;
            avaliacoes.Should().HaveCount(2);
            avaliacoes.Single(a => a.Id == Avaliacao.Id).Tratamentos.Select(t => t.Nome)
                .Should().BeEquivalentTo("Reabilitação de teste", "RPG", "Hidroterapia");
            avaliacoes.Single(a => a.Id == semTratamento.Dados!.Id).Tratamentos.Should().BeEmpty();
        }

        [Fact]
        public async Task Inativar_preserva_o_historico_e_permite_reativar()
        {
            await EvolucaoEm(Tratamento.Id, diasAtras: 2);
            var casoDeUso = CasoDeTratamentos(ComoVeterinario());

            var inativado = await casoDeUso.Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = "inativado" });

            inativado.Sucesso.Should().BeTrue(inativado.Mensagem);
            inativado.Dados!.Status.Should().Be(StatusTratamento.Inativado);
            inativado.Dados.DataFim.Should().NotBeNull();
            inativado.Dados.EvolucoesRegistradas.Should().Be(1, "inativar não apaga nada");
            Contexto.Atendimentos.Count(a => a.TratamentoId == Tratamento.Id).Should().Be(1);

            var reativado = await casoDeUso.Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = StatusTratamento.EmAndamento });

            reativado.Sucesso.Should().BeTrue(reativado.Mensagem);
            reativado.Dados!.DataFim.Should().BeNull();
        }

        [Fact]
        public async Task Tratamento_concluido_nao_e_reaberto()
        {
            var casoDeUso = CasoDeTratamentos(ComoVeterinario());
            await casoDeUso.Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = StatusTratamento.Concluido });

            var reaberto = await casoDeUso.Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = StatusTratamento.EmAndamento });

            reaberto.Sucesso.Should().BeFalse();
            reaberto.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Atualizacao_do_tratamento_altera_so_o_que_foi_enviado()
        {
            var resultado = await CasoDeTratamentos(ComoVeterinario()).Atualizar(Tratamento.Id, new AtualizarTratamentoDTO
            {
                Frequencia = "3 sessões por semana",
                SessoesPrevistas = 24
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Frequencia.Should().Be("3 sessões por semana");
            resultado.Dados.SessoesPrevistas.Should().Be(24);
            resultado.Dados.Nome.Should().Be("Reabilitação de teste");
        }

        // ------------------------------------------------------------------ receitas e exames

        private static CriarPrescricaoDTO ReceitaDe(Guid pacienteId) => new()
        {
            PacienteId = pacienteId,
            Itens = new List<CriarItemPrescricaoDTO>
            {
                new() { Medicamento = "Meloxicam 0,5 mg", Dosagem = "1 comprimido", Frequencia = "a cada 24 horas", Duracao = "5 dias" }
            }
        };

        [Fact]
        public async Task Receita_exige_a_origem()
        {
            var resultado = await CasoDeReceitas(ComoVeterinario()).Emitir(ReceitaDe(Paciente.Id));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("origem da receita");
        }

        [Fact]
        public async Task Receita_nasce_da_avaliacao_ou_da_evolucao()
        {
            var daAvaliacao = ReceitaDe(Paciente.Id);
            daAvaliacao.AvaliacaoId = Avaliacao.Id;

            var primeira = await CasoDeReceitas(ComoVeterinario()).Emitir(daAvaliacao);

            primeira.Sucesso.Should().BeTrue(primeira.Mensagem);
            primeira.Dados!.Origem.Should().StartWith("Avaliação de");

            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);
            var daEvolucao = ReceitaDe(Paciente.Id);
            daEvolucao.AtendimentoId = evolucao.Id;

            var segunda = await CasoDeReceitas(ComoVeterinario()).Emitir(daEvolucao);

            segunda.Sucesso.Should().BeTrue(segunda.Mensagem);
            segunda.Dados!.Origem.Should().Be("Evolução nº 1 — Reabilitação de teste");
        }

        [Fact]
        public async Task Receita_nao_aceita_origem_de_outro_paciente()
        {
            var avaliacaoDoOutro = new AvaliacaoClinica
            {
                PacienteId = _pacienteDoOutro.Id,
                ProntuarioId = Contexto.Prontuarios.Single(p => p.PacienteId == _pacienteDoOutro.Id).Id,
                VeterinarioId = _outroVeterinario.Id,
                MotivoConsulta = "Vômitos",
                Observacoes = "Há dois dias",
                DiagnosticoPossivel = "Gastrite"
            };

            Contexto.AvaliacoesClinicas.Add(avaliacaoDoOutro);
            await Contexto.SaveChangesAsync();

            var dto = ReceitaDe(Paciente.Id);
            dto.AvaliacaoId = avaliacaoDoOutro.Id;

            var resultado = await CasoDeReceitas(ComoAdministrador()).Emitir(new CriarPrescricaoDTO
            {
                PacienteId = dto.PacienteId,
                AvaliacaoId = dto.AvaliacaoId,
                VeterinarioId = Veterinario.Id,
                Itens = dto.Itens
            });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("não pertence a este paciente");
        }

        [Fact]
        public async Task Pedido_de_exame_nasce_da_avaliacao_e_e_cancelado_sem_ser_apagado()
        {
            var casoDeUso = CasoDeExames(ComoVeterinario());

            var emitido = await casoDeUso.Emitir(new CriarPedidoExameDTO
            {
                AvaliacaoId = Avaliacao.Id,
                Exames = "Radiografia do joelho direito\nHemograma completo",
                Justificativa = "Investigar ruptura do ligamento cruzado"
            });

            emitido.Sucesso.Should().BeTrue(emitido.Mensagem);
            emitido.Dados!.PacienteId.Should().Be(Paciente.Id);
            emitido.Dados.MotivoAvaliacao.Should().Be(Avaliacao.MotivoConsulta);

            var cancelado = await casoDeUso.Cancelar(emitido.Dados.Id, "Exame já realizado em outra clínica");

            cancelado.Sucesso.Should().BeTrue(cancelado.Mensagem);
            Contexto.PedidosExame.Single().Status.Should().Be(StatusPedidoExame.Cancelado);
        }

        [Fact]
        public async Task Colega_pede_exame_em_nome_proprio_para_paciente_de_outro_veterinario()
        {
            var resultado = await CasoDeExames(ComoOutroVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                AvaliacaoId = Avaliacao.Id,
                Exames = "Hemograma"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            Contexto.PedidosExame.Single().VeterinarioId.Should().Be(_outroVeterinario.Id,
                "o pedido é assinado por quem o emite");
        }

        // ------------------------------------------------------------------ linha do tempo

        [Fact]
        public async Task Linha_do_tempo_reune_os_eventos_clinicos_do_animal()
        {
            await EvolucaoEm(Tratamento.Id, diasAtras: 3);

            var receita = ReceitaDe(Paciente.Id);
            receita.AvaliacaoId = Avaliacao.Id;
            await CasoDeReceitas(ComoVeterinario()).Emitir(receita);

            await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO { AvaliacaoId = Avaliacao.Id, Exames = "Hemograma" });

            Contexto.Vacinas.Add(new Vacina
            {
                PacienteId = Paciente.Id,
                VeterinarioId = Veterinario.Id,
                Tipo = "Vacina",
                Nome = "Antirrábica",
                DataAplicacao = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-30), DateTimeKind.Utc)
            });

            Contexto.AlergiasCondicoes.Add(new AlergiaCondicao
            {
                PacienteId = Paciente.Id,
                RegistradoPorId = UsuarioVeterinario.Id,
                Tipo = "Cirurgia",
                Descricao = "Reconstrução do ligamento cruzado",
                Gravidade = "Moderada"
            });

            await Contexto.SaveChangesAsync();

            await CasoDeTratamentos(ComoVeterinario())
                .Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = StatusTratamento.Concluido });

            var linha = (await CasoDeProntuario(ComoVeterinario()).Executar(Paciente.Id)).Dados!.Historico;

            linha.Select(e => e.Categoria).Should().Contain(new[]
            {
                LinhaDoTempo.Categorias.Avaliacao,
                LinhaDoTempo.Categorias.Tratamento,
                LinhaDoTempo.Categorias.Evolucao,
                LinhaDoTempo.Categorias.Vacinacao,
                LinhaDoTempo.Categorias.Cirurgia,
                LinhaDoTempo.Categorias.Receita,
                LinhaDoTempo.Categorias.Exame
            });

            linha.Select(e => e.Tipo).Should().Contain(new[] { "Início do tratamento", "Tratamento concluído" });
            linha.Select(e => e.Chave).Should().OnlyHaveUniqueItems();
            linha.Should().BeInDescendingOrder(e => e.Data);

            // Cada evento identifica o registro completo e quem é o responsável.
            var evolucao = linha.Single(e => e.Categoria == LinhaDoTempo.Categorias.Evolucao);
            evolucao.NumeroSessao.Should().Be(1);
            evolucao.TratamentoId.Should().Be(Tratamento.Id);
            evolucao.Autor.Should().Be("Dra. Veterinária");
        }

        // ------------------------------------------------------------------ filtros das evoluções

        [Fact]
        public async Task Evolucoes_de_um_tratamento_vem_so_dele_em_ordem_cronologica()
        {
            var outro = await CasoDeTratamentos(ComoVeterinario()).Cadastrar(TratamentoDe(Avaliacao.Id, "Hidroterapia"));

            await EvolucaoEm(Tratamento.Id, diasAtras: 5, "Sessão A");
            await EvolucaoEm(outro.Dados!.Id, diasAtras: 4, "Sessão do outro tratamento");
            await EvolucaoEm(Tratamento.Id, diasAtras: 3, "Sessão B");

            var resultado = await CasoDeEvolucoes(ComoVeterinario())
                .Listar(null, Tratamento.Id, null, null, null, null, new ParametrosPagina());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Itens.Select(e => e.TecnicasAplicadas).Should().Equal("Sessão A", "Sessão B");
            resultado.Dados.Itens.Select(e => e.NumeroSessao).Should().Equal(1, 2);

            // O histórico completo do animal continua disponível pelo filtro de paciente.
            var doAnimal = await CasoDeEvolucoes(ComoVeterinario())
                .Listar(Paciente.Id, null, null, null, null, null, new ParametrosPagina());

            doAnimal.Dados!.Total.Should().Be(3);
        }

        [Fact]
        public async Task Evolucoes_filtradas_por_periodo_e_numero_da_sessao()
        {
            await EvolucaoEm(Tratamento.Id, diasAtras: 20, "Antiga");
            await EvolucaoEm(Tratamento.Id, diasAtras: 2, "Recente");

            var hoje = RelogioDaClinica.Padrao.Hoje;

            var recentes = await CasoDeEvolucoes(ComoVeterinario())
                .Listar(null, null, hoje.AddDays(-7), hoje, null, null, new ParametrosPagina());

            recentes.Dados!.Itens.Select(e => e.TecnicasAplicadas).Should().Equal("Recente");

            var primeiraSessao = await CasoDeEvolucoes(ComoVeterinario())
                .Listar(null, null, null, null, null, 1, new ParametrosPagina());

            primeiraSessao.Dados!.Itens.Select(e => e.TecnicasAplicadas).Should().Equal("Antiga");

            var invertido = await CasoDeEvolucoes(ComoVeterinario())
                .Listar(null, null, hoje, hoje.AddDays(-7), null, null, new ParametrosPagina());

            invertido.Sucesso.Should().BeFalse();
        }

        [Fact]
        public async Task Evolucoes_filtradas_por_profissional_e_visiveis_a_qualquer_veterinario()
        {
            await EvolucaoEm(Tratamento.Id, diasAtras: 1);

            var porProfissional = await CasoDeEvolucoes(ComoAdministrador())
                .Listar(null, null, null, null, Veterinario.Id, null, new ParametrosPagina());

            porProfissional.Dados!.Total.Should().Be(1);

            // O colega que cobre a ausência do responsável enxerga a evolução do paciente.
            var doOutro = await CasoDeEvolucoes(ComoOutroVeterinario())
                .Listar(Paciente.Id, null, null, null, null, null, new ParametrosPagina());

            doOutro.Sucesso.Should().BeTrue(doOutro.Mensagem);
            doOutro.Dados!.Total.Should().Be(1);
        }

        // ------------------------------------------------------------------ sessão sem agendamento

        private static CriarAtendimentoDTO EvolucaoDoTratamento(Guid tratamentoId) => new()
        {
            TratamentoId = tratamentoId,
            TecnicasAplicadas = "Hidroterapia",
            EscalaDor = 3,
            EvolucaoClinica = "Esteira aquática por 15 minutos",
            ReacaoAnimal = "Tolerou bem"
        };

        [Fact]
        public async Task Sessao_sem_agendamento_e_registrada_direto_no_tratamento()
        {
            var planejado = await CasoDeTratamentos(ComoVeterinario()).Cadastrar(TratamentoDe(Avaliacao.Id, "Hidroterapia"));

            var resultado = await CasoDeEvolucoes(ComoVeterinario()).Executar(EvolucaoDoTratamento(planejado.Dados!.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.NumeroSessao.Should().Be(1);
            resultado.Dados.TratamentoId.Should().Be(planejado.Dados.Id);

            // A sessão nasce concluída, na agenda de quem atendeu e no momento do registro.
            var sessao = await Contexto.Sessoes.FindAsync(resultado.Dados.SessaoId);
            sessao!.Status.Should().Be(StatusSessao.Concluida);
            sessao.VeterinarioId.Should().Be(Veterinario.Id);
            sessao.DataHora.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

            // A primeira evolução tira o tratamento do planejamento.
            (await Contexto.Tratamentos.FindAsync(planejado.Dados.Id))!.Status.Should().Be(StatusTratamento.EmAndamento);

            var segunda = await CasoDeEvolucoes(ComoVeterinario()).Executar(EvolucaoDoTratamento(planejado.Dados.Id));

            segunda.Dados!.NumeroSessao.Should().Be(2);
        }

        [Fact]
        public async Task Colega_registra_sessao_sem_agendamento_em_nome_proprio()
        {
            var resultado = await CasoDeEvolucoes(ComoOutroVeterinario()).Executar(EvolucaoDoTratamento(Tratamento.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioId.Should().Be(_outroVeterinario.Id);
            (await Contexto.Sessoes.FindAsync(resultado.Dados.SessaoId))!.VeterinarioId.Should().Be(_outroVeterinario.Id);
        }

        [Fact]
        public async Task Evolucao_exige_a_sessao_ou_o_tratamento()
        {
            var dto = EvolucaoDoTratamento(Tratamento.Id);
            dto.TratamentoId = null;

            var resultado = await CasoDeEvolucoes(ComoVeterinario()).Executar(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Validacao);
        }

        [Fact]
        public async Task Tratamento_concluido_nao_recebe_sessao_sem_agendamento()
        {
            await CasoDeTratamentos(ComoVeterinario()).Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = StatusTratamento.Concluido });
            var sessoesAntes = Contexto.Sessoes.Count();

            var resultado = await CasoDeEvolucoes(ComoVeterinario()).Executar(EvolucaoDoTratamento(Tratamento.Id));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            Contexto.Sessoes.Count().Should().Be(sessoesAntes, "o registro recusado não deixa sessão na agenda");
        }

        // ------------------------------------------------------------------ telas de cadastro

        [Fact]
        public async Task Pacientes_filtrados_por_raca()
        {
            var resultado = await CasoDePacientes(ComoAdministrador())
                .Listar(null, null, false, null, null, new ParametrosPagina(), raca: "siamês");

            resultado.Dados!.Itens.Select(p => p.Nome).Should().Equal("Paciente do Outro");

            var racas = await CasoDePacientes(ComoAdministrador()).ListarRacas();

            racas.Dados.Should().Equal("Siamês", "SRD");

            // O veterinário vê os pacientes de toda a clínica, e com eles todas as raças.
            (await CasoDePacientes(ComoVeterinario()).ListarRacas()).Dados.Should().Equal("Siamês", "SRD");
        }
    }
}
