using FluentAssertions;
using VetCare.API.Common;
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
    /// Fluxo clínico do animal: Animal → Avaliações → Tratamentos vinculados às avaliações →
    /// Evoluções vinculadas aos tratamentos. Receitas e pedidos de exame nascem de uma avaliação ou
    /// de um tratamento (direto nele ou no registro de uma das sessões dele).
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
            new TratamentoRepository(Contexto),
            new PetRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new VeterinarioRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarPedidosExameUseCase CasoDeExames(UsuarioAtual usuarioAtual) => new(
            new PedidoExameRepository(Contexto),
            new AvaliacaoRepository(Contexto),
            new AtendimentoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new PetRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private ConsultarProntuarioUseCase CasoDeProntuario(UsuarioAtual usuarioAtual) => new(
            new ProntuarioRepository(Contexto),
            new PetRepository(Contexto),
            new TratamentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new ClinicaRepository(Contexto),
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

        private async Task<Sessao> SessaoDe(Guid tratamentoId, int diasAtras, Guid? veterinarioId = null)
        {
            var sessao = new Sessao
            {
                TratamentoId = tratamentoId,
                VeterinarioId = veterinarioId ?? Veterinario.Id,
                DataHora = DateTime.UtcNow.AddDays(-diasAtras),
                DataHoraFim = DateTime.UtcNow.AddDays(-diasAtras).AddHours(1),
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
            primeira.Dados.TipoOrigem.Should().Be(OrigemClinica.Avaliacao);
            primeira.Dados.MotivoAvaliacao.Should().Be(Avaliacao.MotivoConsulta);

            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);
            var daEvolucao = ReceitaDe(Paciente.Id);
            daEvolucao.AtendimentoId = evolucao.Id;

            var segunda = await CasoDeReceitas(ComoVeterinario()).Emitir(daEvolucao);

            segunda.Sucesso.Should().BeTrue(segunda.Mensagem);
            segunda.Dados!.TipoOrigem.Should().Be(OrigemClinica.Tratamento, "a receita feita em uma sessão é do tratamento dela");
            segunda.Dados.Origem.Should().Be("Tratamento Reabilitação de teste");
            segunda.Dados.AtendimentoId.Should().Be(evolucao.Id, "a sessão fica guardada, mas não é mostrada como origem");
            segunda.Dados.TratamentoId.Should().Be(Tratamento.Id, "a receita da sessão é do tratamento da sessão");
            segunda.Dados.NomeTratamento.Should().Be("Reabilitação de teste");
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

        private async Task<PedidoExameDTO> PedidoEmitido()
        {
            var emitido = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                AvaliacaoId = Avaliacao.Id,
                Exames = "Radiografia do joelho direito\nHemograma completo",
                Justificativa = "Investigar ruptura do ligamento cruzado"
            });

            emitido.Sucesso.Should().BeTrue(emitido.Mensagem);
            return emitido.Dados!;
        }

        [Fact]
        public async Task Pedido_de_exame_nasce_da_avaliacao_assinado_por_quem_emite()
        {
            var pedido = await PedidoEmitido();

            pedido.PacienteId.Should().Be(Paciente.Id);
            pedido.MotivoAvaliacao.Should().Be(Avaliacao.MotivoConsulta);
            pedido.VeterinarioId.Should().Be(Veterinario.Id);
        }

        [Fact]
        public async Task Pedido_de_exame_e_editado_e_a_versao_anterior_fica_guardada()
        {
            var pedido = await PedidoEmitido();

            var retorno = await CasoDeAvaliacao(ComoVeterinario()).Executar(new CriarAvaliacaoDTO
            {
                PacienteId = Paciente.Id,
                MotivoConsulta = "Retorno com claudicação",
                Observacoes = "Piora após exercício",
                DiagnosticoPossivel = "Lesão meniscal"
            });

            var editado = await CasoDeExames(ComoVeterinario()).Atualizar(pedido.Id, new AtualizarPedidoExameDTO
            {
                AvaliacaoId = retorno.Dados!.Id,
                Exames = "Ultrassonografia do joelho direito",
                Justificativa = "Avaliar menisco"
            });

            editado.Sucesso.Should().BeTrue(editado.Mensagem);
            editado.Dados!.Exames.Should().Be("Ultrassonografia do joelho direito");
            editado.Dados.AvaliacaoId.Should().Be(retorno.Dados.Id);
            editado.Dados.MotivoAvaliacao.Should().Be("Retorno com claudicação");

            Contexto.VersoesRegistrosClinicos.Should().ContainSingle(v =>
                v.TipoRegistro == GerenciarPedidosExameUseCase.TipoRegistro && v.RegistroId == pedido.Id &&
                v.ConteudoAnterior.Contains("Hemograma completo"));
            Contexto.RegistrosAuditoria.Should().Contain(r =>
                r.Acao == AuditoriaService.Acoes.Alteracao && r.EntidadeId == pedido.Id);
        }

        [Fact]
        public async Task Pedido_de_exame_nao_passa_para_avaliacao_de_outro_animal()
        {
            var pedido = await PedidoEmitido();

            var deOutroAnimal = await CasoDeAvaliacao(ComoOutroVeterinario()).Executar(new CriarAvaliacaoDTO
            {
                PacienteId = _pacienteDoOutro.Id,
                MotivoConsulta = "Check-up",
                Observacoes = "Sem queixas",
                DiagnosticoPossivel = "Saudável"
            });

            var resultado = await CasoDeExames(ComoVeterinario()).Atualizar(pedido.Id, new AtualizarPedidoExameDTO
            {
                AvaliacaoId = deOutroAnimal.Dados!.Id,
                Exames = "Hemograma"
            });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("mesmo animal");
        }

        [Fact]
        public async Task Pedido_de_exame_excluido_sai_do_prontuario_e_fica_na_auditoria()
        {
            var pedido = await PedidoEmitido();

            var resultado = await CasoDeExames(ComoVeterinario()).Excluir(pedido.Id);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            Contexto.PedidosExame.Should().BeEmpty("a exclusão tira o pedido do sistema, não o deixa cancelado");

            var prontuario = await CasoDeProntuario(ComoVeterinario()).Executar(Paciente.Id);
            prontuario.Dados!.PedidosExame.Should().BeEmpty();
            prontuario.Dados.Historico.Should().NotContain(e => e.Categoria == LinhaDoTempo.Categorias.Exame);

            Contexto.RegistrosAuditoria.Should().Contain(r =>
                r.Acao == AuditoriaService.Acoes.Exclusao && r.EntidadeId == pedido.Id && r.Detalhe.Contains("Hemograma completo"));
        }

        [Fact]
        public async Task Colega_nao_edita_nem_exclui_pedido_de_exame_de_outro_veterinario()
        {
            var pedido = await PedidoEmitido();
            var casoDoColega = CasoDeExames(ComoOutroVeterinario());

            var edicao = await casoDoColega.Atualizar(pedido.Id, new AtualizarPedidoExameDTO { Exames = "Hemograma" });
            var exclusao = await casoDoColega.Excluir(pedido.Id);

            edicao.Falha.Should().Be(TipoFalha.NaoAutorizado);
            exclusao.Falha.Should().Be(TipoFalha.NaoAutorizado);
            Contexto.PedidosExame.Should().ContainSingle();

            // A administração pode corrigir e excluir qualquer pedido da clínica.
            (await CasoDeExames(ComoAdministrador()).Excluir(pedido.Id)).Sucesso.Should().BeTrue();
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

        // ------------------------------------------------------------------ vínculo com o tratamento

        private async Task<Tratamento> OutroTratamento(Guid pacienteId, Guid avaliacaoId, string status = StatusTratamento.EmAndamento)
        {
            var tratamento = new Tratamento
            {
                PacienteId = pacienteId,
                AvaliacaoId = avaliacaoId,
                VeterinarioId = Veterinario.Id,
                DataInicio = DateTime.UtcNow.AddDays(-5),
                Nome = "Hidroterapia",
                Tipo = "Hidroterapia",
                Descricao = "Esteira aquática",
                SessoesPrevistas = 8,
                Status = status
            };

            Contexto.Tratamentos.Add(tratamento);
            await Contexto.SaveChangesAsync();
            return tratamento;
        }

        [Fact]
        public async Task Pedido_de_exame_nasce_da_sessao_ligado_ao_tratamento_dela()
        {
            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);

            var resultado = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                AtendimentoId = evolucao.Id,
                Exames = "Radiografia do joelho direito",
                Justificativa = "Dor após a sessão"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            var pedido = resultado.Dados!;
            pedido.AtendimentoId.Should().Be(evolucao.Id);
            pedido.NumeroSessao.Should().Be(1);
            pedido.TipoOrigem.Should().Be(OrigemClinica.Tratamento);
            pedido.Origem.Should().Be("Tratamento Reabilitação de teste");
            pedido.TratamentoId.Should().Be(Tratamento.Id);
            pedido.NomeTratamento.Should().Be("Reabilitação de teste");
            pedido.AvaliacaoId.Should().BeNull("a origem do pedido feito na sessão é o tratamento, não a avaliação");

            var prontuario = await CasoDeProntuario(ComoVeterinario()).Executar(Paciente.Id);
            prontuario.Dados!.Avaliacoes.Single().QuantidadePedidosExame.Should().Be(0,
                "o pedido feito na sessão não conta como pedido da avaliação");
            prontuario.Dados.Historico.Single(e => e.Categoria == LinhaDoTempo.Categorias.Exame)
                .TratamentoId.Should().Be(Tratamento.Id);
        }

        [Fact]
        public async Task Pedido_de_exame_exige_uma_unica_origem()
        {
            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);

            var semOrigem = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO { Exames = "Hemograma" });
            var duasOrigens = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                AvaliacaoId = Avaliacao.Id,
                AtendimentoId = evolucao.Id,
                Exames = "Hemograma"
            });

            semOrigem.Falha.Should().Be(TipoFalha.Validacao);
            duasOrigens.Falha.Should().Be(TipoFalha.Validacao);
            semOrigem.Mensagem.Should().Contain("origem do pedido");
            Contexto.PedidosExame.Should().BeEmpty();
        }

        [Fact]
        public async Task Receita_e_pedido_da_sessao_ficam_no_tratamento_da_sessao()
        {
            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);
            var outro = await OutroTratamento(Paciente.Id, Avaliacao.Id);

            var receita = ReceitaDe(Paciente.Id);
            receita.AtendimentoId = evolucao.Id;
            receita.TratamentoId = outro.Id;

            var daReceita = await CasoDeReceitas(ComoVeterinario()).Emitir(receita);
            var doPedido = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                AtendimentoId = evolucao.Id,
                TratamentoId = outro.Id,
                Exames = "Hemograma"
            });

            daReceita.Falha.Should().Be(TipoFalha.Validacao);
            daReceita.Mensagem.Should().Be(VinculoComTratamento.MensagemDaSessao);
            doPedido.Falha.Should().Be(TipoFalha.Validacao);
            Contexto.Prescricoes.Should().BeEmpty();
            Contexto.PedidosExame.Should().BeEmpty();
        }

        [Fact]
        public async Task Receita_da_avaliacao_pode_ser_ligada_a_um_tratamento_ativo_do_animal()
        {
            var comTratamento = ReceitaDe(Paciente.Id);
            comTratamento.AvaliacaoId = Avaliacao.Id;
            comTratamento.TratamentoId = Tratamento.Id;

            var ligada = await CasoDeReceitas(ComoVeterinario()).Emitir(comTratamento);

            ligada.Sucesso.Should().BeTrue(ligada.Mensagem);
            ligada.Dados!.TratamentoId.Should().Be(Tratamento.Id);
            ligada.Dados.NomeTratamento.Should().Be("Reabilitação de teste");
            Contexto.RegistrosAuditoria.Should().Contain(r =>
                r.EntidadeId == ligada.Dados.Id && r.Detalhe.Contains("para o tratamento Reabilitação de teste"));

            var semTratamento = ReceitaDe(Paciente.Id);
            semTratamento.AvaliacaoId = Avaliacao.Id;

            var avulsa = await CasoDeReceitas(ComoVeterinario()).Emitir(semTratamento);

            avulsa.Sucesso.Should().BeTrue(avulsa.Mensagem);
            avulsa.Dados!.TratamentoId.Should().BeNull("na avaliação, o vínculo com um tratamento é opcional");
            avulsa.Dados.NomeTratamento.Should().BeEmpty();
        }

        [Fact]
        public async Task Receita_nao_se_liga_a_tratamento_de_outro_animal_nem_a_tratamento_encerrado()
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

            var deOutroAnimal = await OutroTratamento(_pacienteDoOutro.Id, avaliacaoDoOutro.Id);
            var concluido = await OutroTratamento(Paciente.Id, Avaliacao.Id, StatusTratamento.Concluido);

            var receitaDoOutro = ReceitaDe(Paciente.Id);
            receitaDoOutro.AvaliacaoId = Avaliacao.Id;
            receitaDoOutro.TratamentoId = deOutroAnimal.Id;

            var receitaEncerrada = ReceitaDe(Paciente.Id);
            receitaEncerrada.AvaliacaoId = Avaliacao.Id;
            receitaEncerrada.TratamentoId = concluido.Id;

            var primeira = await CasoDeReceitas(ComoVeterinario()).Emitir(receitaDoOutro);
            var segunda = await CasoDeReceitas(ComoVeterinario()).Emitir(receitaEncerrada);

            primeira.Falha.Should().Be(TipoFalha.Validacao);
            primeira.Mensagem.Should().Contain("não pertence a este paciente");
            segunda.Falha.Should().Be(TipoFalha.Validacao);
            segunda.Mensagem.Should().Contain("planejado ou em andamento");
            Contexto.Prescricoes.Should().BeEmpty();
        }

        [Fact]
        public async Task Pedido_da_avaliacao_corrigido_troca_ou_tira_o_tratamento()
        {
            var pedido = await PedidoEmitido();
            pedido.TratamentoId.Should().BeNull();

            var ligado = await CasoDeExames(ComoVeterinario()).Atualizar(pedido.Id, new AtualizarPedidoExameDTO
            {
                Exames = pedido.Exames,
                TratamentoId = Tratamento.Id
            });

            ligado.Sucesso.Should().BeTrue(ligado.Mensagem);
            ligado.Dados!.NomeTratamento.Should().Be("Reabilitação de teste");

            // Concluído depois, o tratamento já ligado continua aceito numa nova correção.
            Tratamento.Status = StatusTratamento.Concluido;
            await Contexto.SaveChangesAsync();

            var mantido = await CasoDeExames(ComoVeterinario()).Atualizar(pedido.Id, new AtualizarPedidoExameDTO
            {
                Exames = "Hemograma completo",
                TratamentoId = Tratamento.Id
            });

            mantido.Sucesso.Should().BeTrue(mantido.Mensagem);
            mantido.Dados!.TratamentoId.Should().Be(Tratamento.Id);

            var desligado = await CasoDeExames(ComoVeterinario()).Atualizar(pedido.Id, new AtualizarPedidoExameDTO
            {
                Exames = "Hemograma completo"
            });

            desligado.Sucesso.Should().BeTrue(desligado.Mensagem);
            desligado.Dados!.TratamentoId.Should().BeNull();
            Contexto.VersoesRegistrosClinicos.Count(v => v.RegistroId == pedido.Id).Should().Be(3);
        }

        [Fact]
        public async Task Pedido_da_sessao_nao_troca_de_origem_nem_de_tratamento()
        {
            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);

            var emitido = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                AtendimentoId = evolucao.Id,
                Exames = "Hemograma"
            });

            var retorno = await CasoDeAvaliacao(ComoVeterinario()).Executar(new CriarAvaliacaoDTO
            {
                PacienteId = Paciente.Id,
                MotivoConsulta = "Retorno",
                Observacoes = "Sem dor",
                DiagnosticoPossivel = "Evolução favorável"
            });

            var outraOrigem = await CasoDeExames(ComoVeterinario()).Atualizar(emitido.Dados!.Id, new AtualizarPedidoExameDTO
            {
                AvaliacaoId = retorno.Dados!.Id,
                Exames = "Hemograma"
            });

            outraOrigem.Falha.Should().Be(TipoFalha.Validacao);
            outraOrigem.Mensagem.Should().Contain("feito em uma sessão");

            var corrigido = await CasoDeExames(ComoVeterinario()).Atualizar(emitido.Dados.Id, new AtualizarPedidoExameDTO
            {
                Exames = "Hemograma completo"
            });

            corrigido.Sucesso.Should().BeTrue(corrigido.Mensagem);
            corrigido.Dados!.Exames.Should().Be("Hemograma completo");
            corrigido.Dados.AtendimentoId.Should().Be(evolucao.Id);
            corrigido.Dados.TratamentoId.Should().Be(Tratamento.Id, "o pedido da sessão fica com o tratamento da sessão");
        }

        // ------------------------------------------------------------------ origem no tratamento

        [Fact]
        public async Task Receita_e_pedido_nascem_no_proprio_tratamento()
        {
            var receita = ReceitaDe(Paciente.Id);
            receita.TratamentoId = Tratamento.Id;

            var daReceita = await CasoDeReceitas(ComoVeterinario()).Emitir(receita);
            var doPedido = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                TratamentoId = Tratamento.Id,
                Exames = "Ultrassonografia do joelho"
            });

            daReceita.Sucesso.Should().BeTrue(daReceita.Mensagem);
            daReceita.Dados!.TipoOrigem.Should().Be(OrigemClinica.Tratamento);
            daReceita.Dados.Origem.Should().Be("Tratamento Reabilitação de teste");
            daReceita.Dados.AvaliacaoId.Should().BeNull();
            daReceita.Dados.AtendimentoId.Should().BeNull();
            daReceita.Dados.TratamentoId.Should().Be(Tratamento.Id);

            doPedido.Sucesso.Should().BeTrue(doPedido.Mensagem);
            doPedido.Dados!.TipoOrigem.Should().Be(OrigemClinica.Tratamento);
            doPedido.Dados.AvaliacaoId.Should().BeNull("o pedido do tratamento não é da avaliação");
            doPedido.Dados.TratamentoId.Should().Be(Tratamento.Id);
            doPedido.Dados.PacienteId.Should().Be(Paciente.Id, "o paciente vem do tratamento");
            doPedido.Dados.VeterinarioId.Should().Be(Veterinario.Id);

            Contexto.RegistrosAuditoria.Should().Contain(r =>
                r.EntidadeId == daReceita.Dados.Id && r.Detalhe.Contains("emitida no tratamento Reabilitação de teste"));

            // Os documentos do tratamento não contam como da avaliação que o originou.
            var prontuario = (await CasoDeProntuario(ComoVeterinario()).Executar(Paciente.Id)).Dados!;
            prontuario.Avaliacoes.Single().QuantidadeReceitas.Should().Be(0);
            prontuario.Avaliacoes.Single().QuantidadePedidosExame.Should().Be(0);

            var eventoDaReceita = prontuario.Historico.Single(e => e.Categoria == LinhaDoTempo.Categorias.Receita);
            eventoDaReceita.Detalhes.Should().Be("Emitida no tratamento Reabilitação de teste");
            eventoDaReceita.Campos["Origem"].Should().Be("Tratamento Reabilitação de teste");
            eventoDaReceita.Campos.Should().NotContainKey("Tratamento", "a origem já diz qual é o tratamento");
            eventoDaReceita.TratamentoId.Should().Be(Tratamento.Id);

            prontuario.Historico.Single(e => e.Categoria == LinhaDoTempo.Categorias.Exame)
                .Detalhes.Should().Be("Pedido feito no tratamento Reabilitação de teste");
        }

        [Fact]
        public async Task Receita_da_avaliacao_mostra_a_avaliacao_como_origem_na_linha_do_tempo()
        {
            var receita = ReceitaDe(Paciente.Id);
            receita.AvaliacaoId = Avaliacao.Id;
            receita.TratamentoId = Tratamento.Id;

            var emitida = await CasoDeReceitas(ComoVeterinario()).Emitir(receita);
            emitida.Sucesso.Should().BeTrue(emitida.Mensagem);

            var prontuario = (await CasoDeProntuario(ComoVeterinario()).Executar(Paciente.Id)).Dados!;
            prontuario.Avaliacoes.Single().QuantidadeReceitas.Should().Be(1);

            var evento = prontuario.Historico.Single(e => e.Categoria == LinhaDoTempo.Categorias.Receita);
            evento.Detalhes.Should().StartWith("Emitida na avaliação de ");
            evento.Campos["Origem"].Should().StartWith("Avaliação de ");
            evento.Campos["Tratamento"].Should().Be("Reabilitação de teste", "a receita da avaliação ligada a um tratamento mostra os dois");
        }

        [Fact]
        public async Task Tratamento_encerrado_ou_de_outro_animal_nao_recebe_receita_nem_pedido()
        {
            var concluido = await OutroTratamento(Paciente.Id, Avaliacao.Id, StatusTratamento.Concluido);
            var inativado = await OutroTratamento(Paciente.Id, Avaliacao.Id, StatusTratamento.Inativado);

            var receitaEncerrada = ReceitaDe(Paciente.Id);
            receitaEncerrada.TratamentoId = concluido.Id;

            var receitaDeOutroAnimal = ReceitaDe(_pacienteDoOutro.Id);
            receitaDeOutroAnimal.TratamentoId = Tratamento.Id;
            receitaDeOutroAnimal.VeterinarioId = _outroVeterinario.Id;

            var primeira = await CasoDeReceitas(ComoVeterinario()).Emitir(receitaEncerrada);
            var segunda = await CasoDeReceitas(ComoOutroVeterinario()).Emitir(receitaDeOutroAnimal);
            var pedido = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                TratamentoId = inativado.Id,
                Exames = "Hemograma"
            });

            primeira.Falha.Should().Be(TipoFalha.Validacao);
            primeira.Mensagem.Should().Contain("planejados ou em andamento");
            segunda.Falha.Should().Be(TipoFalha.Validacao);
            segunda.Mensagem.Should().Contain("não pertence a este paciente");
            pedido.Falha.Should().Be(TipoFalha.Validacao);
            pedido.Mensagem.Should().Contain("inativado");
            Contexto.Prescricoes.Should().BeEmpty();
            Contexto.PedidosExame.Should().BeEmpty();
        }

        [Fact]
        public async Task Receita_nao_aceita_avaliacao_e_sessao_juntas()
        {
            var evolucao = await EvolucaoEm(Tratamento.Id, diasAtras: 1);
            var receita = ReceitaDe(Paciente.Id);
            receita.AvaliacaoId = Avaliacao.Id;
            receita.AtendimentoId = evolucao.Id;

            var resultado = await CasoDeReceitas(ComoVeterinario()).Emitir(receita);

            resultado.Falha.Should().Be(TipoFalha.Validacao);
            resultado.Mensagem.Should().Contain("origem da receita");
            Contexto.Prescricoes.Should().BeEmpty();
        }

        [Fact]
        public async Task Pedido_do_tratamento_emitido_pela_administracao_e_assinado_pelo_responsavel()
        {
            var tratamentoDoColega = await OutroTratamento(Paciente.Id, Avaliacao.Id);
            tratamentoDoColega.VeterinarioId = _outroVeterinario.Id;
            await Contexto.SaveChangesAsync();

            var resultado = await CasoDeExames(ComoAdministrador()).Emitir(new CriarPedidoExameDTO
            {
                TratamentoId = tratamentoDoColega.Id,
                Exames = "Hemograma"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioId.Should().Be(_outroVeterinario.Id, "sem veterinário logado, assina o responsável pelo tratamento");
        }

        [Fact]
        public async Task Pedido_do_tratamento_nao_passa_para_uma_avaliacao()
        {
            var emitido = await CasoDeExames(ComoVeterinario()).Emitir(new CriarPedidoExameDTO
            {
                TratamentoId = Tratamento.Id,
                Exames = "Hemograma"
            });

            var outraOrigem = await CasoDeExames(ComoVeterinario()).Atualizar(emitido.Dados!.Id, new AtualizarPedidoExameDTO
            {
                AvaliacaoId = Avaliacao.Id,
                Exames = "Hemograma"
            });

            outraOrigem.Falha.Should().Be(TipoFalha.Validacao);
            outraOrigem.Mensagem.Should().Contain("feito no tratamento");

            var corrigido = await CasoDeExames(ComoVeterinario()).Atualizar(emitido.Dados.Id, new AtualizarPedidoExameDTO
            {
                Exames = "Hemograma completo"
            });

            corrigido.Sucesso.Should().BeTrue(corrigido.Mensagem);
            corrigido.Dados!.TratamentoId.Should().Be(Tratamento.Id, "o pedido feito no tratamento continua nele");
            corrigido.Dados.AvaliacaoId.Should().BeNull();
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
                LinhaDoTempo.Categorias.Sessao,
                LinhaDoTempo.Categorias.Vacinacao,
                LinhaDoTempo.Categorias.Cirurgia,
                LinhaDoTempo.Categorias.Receita,
                LinhaDoTempo.Categorias.Exame
            });

            linha.Select(e => e.Tipo).Should().Contain(new[] { "Início do tratamento", "Tratamento concluído" });
            linha.Select(e => e.Chave).Should().OnlyHaveUniqueItems();
            linha.Should().BeInDescendingOrder(e => e.Data);

            // Cada evento identifica o registro completo e quem é o responsável.
            var evolucao = linha.Single(e => e.Categoria == LinhaDoTempo.Categorias.Sessao);
            evolucao.Tipo.Should().Be("Sessão nº 1");
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

        // ------------------------------------------------------------------ registro do atendimento

        private static CriarAtendimentoDTO AtendimentoDa(Guid? sessaoId) => new()
        {
            SessaoId = sessaoId,
            TecnicasAplicadas = "Hidroterapia",
            EscalaDor = 3,
            EvolucaoClinica = "Esteira aquática por 15 minutos",
            ReacaoAnimal = "Tolerou bem"
        };

        [Fact]
        public async Task Registro_do_atendimento_exige_uma_sessao_cadastrada()
        {
            var sessoesAntes = Contexto.Sessoes.Count();

            var semSessao = await CasoDeEvolucoes(ComoVeterinario()).Executar(AtendimentoDa(null));

            semSessao.Sucesso.Should().BeFalse();
            semSessao.Falha.Should().Be(TipoFalha.Validacao);
            semSessao.Mensagem.Should().Contain("cadastradas no tratamento");
            Contexto.Sessoes.Count().Should().Be(sessoesAntes, "o registro nunca cria sessão: elas nascem só do cadastro");
        }

        [Fact]
        public async Task Colega_registra_o_atendimento_da_sessao_da_propria_agenda_em_nome_proprio()
        {
            // A sessão do paciente da colega está na agenda de quem cobre a ausência.
            var sessao = await SessaoDe(Tratamento.Id, diasAtras: 0, veterinarioId: _outroVeterinario.Id);

            var resultado = await CasoDeEvolucoes(ComoOutroVeterinario()).Executar(AtendimentoDa(sessao.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioId.Should().Be(_outroVeterinario.Id);
            resultado.Dados.SessaoId.Should().Be(sessao.Id);
        }

        [Fact]
        public async Task Tratamento_concluido_nao_recebe_registro_de_atendimento()
        {
            var sessao = await SessaoDe(Tratamento.Id, diasAtras: 1);
            await CasoDeTratamentos(ComoVeterinario()).Atualizar(Tratamento.Id, new AtualizarTratamentoDTO { Status = StatusTratamento.Concluido });

            var resultado = await CasoDeEvolucoes(ComoVeterinario()).Executar(AtendimentoDa(sessao.Id));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            Contexto.Atendimentos.Count().Should().Be(0);
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
