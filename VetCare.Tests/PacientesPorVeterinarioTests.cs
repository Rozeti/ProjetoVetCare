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
    /// Cada paciente é acompanhado por um único veterinário responsável. O profissional
    /// enxerga só os seus; a transferência leva junto o tratamento em andamento e as
    /// sessões futuras, e o paciente some da lista de quem o entregou.
    /// </summary>
    public class PacientesPorVeterinarioTests : BaseDeTeste
    {
        private readonly Usuario _usuarioOutroVeterinario;
        private readonly Veterinario _outroVeterinario;

        public PacientesPorVeterinarioTests()
        {
            _usuarioOutroVeterinario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Dr. Outro Veterinário",
                Email = "outro.vet@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash",
                Ativo = true
            };

            _outroVeterinario = new Veterinario
            {
                UsuarioId = _usuarioOutroVeterinario.Id,
                Crmv = "CRMV-DF 2000",
                Especialidade = "Ortopedia"
            };

            Contexto.Usuarios.Add(_usuarioOutroVeterinario);
            Contexto.Veterinarios.Add(_outroVeterinario);
            Contexto.SaveChanges();
        }

        private UsuarioAtual ComoOutroVeterinario() =>
            ComoUsuario(_usuarioOutroVeterinario, Clinica.Id, veterinarioId: _outroVeterinario.Id);

        private GerenciarPacientesUseCase CasoDePacientes(UsuarioAtual usuarioAtual) => new(
            new PetRepository(Contexto),
            new TutorRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new AlergiaRepository(Contexto),
            new VacinaRepository(Contexto),
            new TratamentoRepository(Contexto),
            new VeterinarioRepository(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private TransferirPacienteUseCase CasoDeTransferencia(UsuarioAtual usuarioAtual) => new(
            new PetRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new TratamentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new BloqueioAgendaRepository(Contexto),
            new ClinicaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarTratamentosUseCase CasoDeTratamentos(UsuarioAtual usuarioAtual) => new(
            new TratamentoRepository(Contexto),
            new PetRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new SessaoRepository(Contexto),
            usuarioAtual);

        private static ParametrosPagina Pagina() => new() { Pagina = 1, Tamanho = 50 };

        private CriarPetDTO PacienteNovo(string nome, Guid? veterinarioResponsavelId = null) => new()
        {
            Nome = nome,
            Especie = "Cachorro",
            Raca = "SRD",
            Sexo = "Macho",
            DataNascimento = new DateTime(2020, 3, 1),
            TutorId = Tutor.Id,
            VeterinarioResponsavelId = veterinarioResponsavelId
        };

        private Sessao SessaoFutura(Guid tratamentoId, Guid veterinarioId, DateTime dataHora)
        {
            var sessao = new Sessao
            {
                TratamentoId = tratamentoId,
                VeterinarioId = veterinarioId,
                DataHora = dataHora,
                Status = StatusSessao.Confirmada
            };

            Contexto.Sessoes.Add(sessao);
            Contexto.SaveChanges();

            return sessao;
        }

        [Fact]
        public async Task Cada_veterinario_lista_apenas_os_proprios_pacientes()
        {
            await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Bolt", _outroVeterinario.Id));

            var doPrimeiro = await CasoDePacientes(ComoVeterinario()).Listar(null, null, false, null, null, Pagina());
            var doSegundo = await CasoDePacientes(ComoOutroVeterinario()).Listar(null, null, false, null, null, Pagina());
            var daClinica = await CasoDePacientes(ComoAdministrador()).Listar(null, null, false, null, null, Pagina());

            doPrimeiro.Dados!.Itens.Select(p => p.Nome).Should().BeEquivalentTo(new[] { Paciente.Nome });
            doSegundo.Dados!.Itens.Select(p => p.Nome).Should().BeEquivalentTo(new[] { "Bolt" });
            daClinica.Dados!.Itens.Should().HaveCount(2);
        }

        [Fact]
        public async Task Filtro_de_veterinario_na_url_nao_amplia_o_recorte_do_proprio_veterinario()
        {
            await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Bolt", _outroVeterinario.Id));

            var resultado = await CasoDePacientes(ComoVeterinario())
                .Listar(null, _outroVeterinario.Id, false, null, null, Pagina());

            resultado.Dados!.Itens.Select(p => p.Nome).Should().BeEquivalentTo(new[] { Paciente.Nome });
        }

        [Fact]
        public async Task Administracao_filtra_pacientes_sem_responsavel()
        {
            await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Sem Dono Clínico"));

            var semResponsavel = await CasoDePacientes(ComoAdministrador()).Listar(null, null, true, null, null, Pagina());

            semResponsavel.Dados!.Itens.Should().ContainSingle(p => p.Nome == "Sem Dono Clínico");
            semResponsavel.Dados.Itens.Should().OnlyContain(p => p.VeterinarioResponsavelId == null);
        }

        [Fact]
        public async Task Veterinario_nao_abre_paciente_de_outro_profissional()
        {
            var negado = await CasoDePacientes(ComoOutroVeterinario()).ObterPorId(Paciente.Id);
            var permitido = await CasoDePacientes(ComoVeterinario()).ObterPorId(Paciente.Id);
            var administracao = await CasoDePacientes(ComoAdministrador()).ObterPorId(Paciente.Id);

            negado.Sucesso.Should().BeFalse();
            negado.Falha.Should().Be(TipoFalha.NaoAutorizado);
            permitido.Sucesso.Should().BeTrue(permitido.Mensagem);
            administracao.Sucesso.Should().BeTrue(administracao.Mensagem);
        }

        [Fact]
        public async Task Veterinario_que_cadastra_assume_o_paciente_mesmo_que_indique_outro()
        {
            var resultado = await CasoDePacientes(ComoVeterinario()).Cadastrar(PacienteNovo("Mel", _outroVeterinario.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioResponsavelId.Should().Be(Veterinario.Id);
            resultado.Dados.NomeVeterinarioResponsavel.Should().Be(UsuarioVeterinario.Nome);
        }

        [Fact]
        public async Task Administracao_escolhe_o_responsavel_no_cadastro()
        {
            var resultado = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Mel", _outroVeterinario.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
        }

        [Fact]
        public async Task Transferencia_leva_tratamento_em_andamento_e_sessoes_futuras()
        {
            var futura = SessaoFutura(Tratamento.Id, Veterinario.Id, DateTime.UtcNow.AddDays(2));
            var passada = SessaoFutura(Tratamento.Id, Veterinario.Id, DateTime.UtcNow.AddDays(-2));

            var resultado = await CasoDeTransferencia(ComoAdministrador())
                .Executar(Paciente.Id, new TransferirPacienteDTO { VeterinarioId = _outroVeterinario.Id, Motivo = "Férias" });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.TratamentosTransferidos.Should().Be(1);
            resultado.Dados.SessoesTransferidas.Should().Be(1);
            resultado.Dados.NomeNovoVeterinario.Should().Be(_usuarioOutroVeterinario.Nome);

            Contexto.ChangeTracker.Clear();

            Contexto.Pets.Single(p => p.Id == Paciente.Id).VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
            Contexto.Tratamentos.Single(t => t.Id == Tratamento.Id).VeterinarioId.Should().Be(_outroVeterinario.Id);
            Contexto.Sessoes.Single(s => s.Id == futura.Id).VeterinarioId.Should().Be(_outroVeterinario.Id);
            Contexto.Sessoes.Single(s => s.Id == passada.Id).VeterinarioId.Should().Be(Veterinario.Id,
                "o histórico continua assinado por quem o produziu");

            // Quem entregou perde o acesso; quem recebeu passa a ter.
            (await CasoDePacientes(ComoVeterinario()).ObterPorId(Paciente.Id)).Falha.Should().Be(TipoFalha.NaoAutorizado);
            (await CasoDePacientes(ComoOutroVeterinario()).ObterPorId(Paciente.Id)).Sucesso.Should().BeTrue();

            Contexto.Notificacoes.Should().Contain(n => n.UsuarioId == _usuarioOutroVeterinario.Id && n.Tipo == TiposDeNotificacao.PacienteTransferido);
            Contexto.Notificacoes.Should().Contain(n => n.UsuarioId == UsuarioVeterinario.Id && n.Tipo == TiposDeNotificacao.PacienteTransferido);
            Contexto.Notificacoes.Should().Contain(n => n.UsuarioId == UsuarioTutor.Id && n.Tipo == TiposDeNotificacao.PacienteTransferido);
        }

        [Fact]
        public async Task Transferencia_e_recusada_quando_a_agenda_do_novo_veterinario_ja_esta_ocupada()
        {
            var horario = DateTime.UtcNow.AddDays(3).Date.AddHours(14);
            SessaoFutura(Tratamento.Id, Veterinario.Id, horario);

            // O outro veterinário já tem compromisso no mesmo horário, com outro paciente.
            var outroPaciente = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Bolt", _outroVeterinario.Id));

            var outroTratamento = new Tratamento
            {
                PacienteId = outroPaciente.Dados!.Id,
                VeterinarioId = _outroVeterinario.Id,
                DataInicio = DateTime.UtcNow.AddDays(-1),
                ObjetivoTerapeutico = "Reabilitação",
                Status = StatusTratamento.EmAndamento
            };

            Contexto.Tratamentos.Add(outroTratamento);
            Contexto.SaveChanges();
            SessaoFutura(outroTratamento.Id, _outroVeterinario.Id, horario);

            var resultado = await CasoDeTransferencia(ComoAdministrador())
                .Executar(Paciente.Id, new TransferirPacienteDTO { VeterinarioId = _outroVeterinario.Id });

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            resultado.Mensagem.Should().Contain("Reagende");

            Contexto.ChangeTracker.Clear();
            Contexto.Pets.Single(p => p.Id == Paciente.Id).VeterinarioResponsavelId.Should().Be(Veterinario.Id,
                "nada muda quando a transferência é recusada");
        }

        [Fact]
        public async Task Veterinario_so_transfere_paciente_que_esta_com_ele()
        {
            var negado = await CasoDeTransferencia(ComoOutroVeterinario())
                .Executar(Paciente.Id, new TransferirPacienteDTO { VeterinarioId = _outroVeterinario.Id });

            negado.Sucesso.Should().BeFalse();
            negado.Falha.Should().Be(TipoFalha.NaoAutorizado);

            var permitido = await CasoDeTransferencia(ComoVeterinario())
                .Executar(Paciente.Id, new TransferirPacienteDTO { VeterinarioId = _outroVeterinario.Id });

            permitido.Sucesso.Should().BeTrue(permitido.Mensagem);
        }

        [Fact]
        public async Task Transferir_para_o_mesmo_veterinario_e_recusado()
        {
            var resultado = await CasoDeTransferencia(ComoAdministrador())
                .Executar(Paciente.Id, new TransferirPacienteDTO { VeterinarioId = Veterinario.Id });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("já é o responsável");
        }

        [Fact]
        public async Task Tratamento_com_veterinario_diferente_do_responsavel_e_recusado()
        {
            var resultado = await CasoDeTratamentos(ComoAdministrador()).Cadastrar(new CriarTratamentoDTO
            {
                PacienteId = Paciente.Id,
                VeterinarioId = _outroVeterinario.Id,
                ObjetivoTerapeutico = "Fortalecimento"
            });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("Transfira o paciente");
        }

        [Fact]
        public async Task Primeiro_tratamento_aberto_pela_administracao_define_quem_acompanha_o_paciente()
        {
            // Paciente cadastrado pelo tutor no aplicativo: ninguém o acompanha ainda, e por
            // isso nenhum veterinário o enxerga; a administração abre o tratamento escolhendo quem será.
            var semResponsavel = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Sem Dono Clínico"));

            var negado = await CasoDeTratamentos(ComoOutroVeterinario()).Cadastrar(new CriarTratamentoDTO
            {
                PacienteId = semResponsavel.Dados!.Id,
                ObjetivoTerapeutico = "Fortalecimento"
            });

            negado.Falha.Should().Be(TipoFalha.NaoAutorizado);

            var resultado = await CasoDeTratamentos(ComoAdministrador()).Cadastrar(new CriarTratamentoDTO
            {
                PacienteId = semResponsavel.Dados.Id,
                VeterinarioId = _outroVeterinario.Id,
                ObjetivoTerapeutico = "Fortalecimento"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            Contexto.ChangeTracker.Clear();
            Contexto.Pets.Single(p => p.Id == semResponsavel.Dados.Id).VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);

            // A partir daí o paciente aparece para quem o recebeu.
            (await CasoDePacientes(ComoOutroVeterinario()).ObterPorId(semResponsavel.Dados.Id)).Sucesso.Should().BeTrue();
        }

        [Fact]
        public async Task Indicador_de_pacientes_ativos_respeita_o_recorte_do_veterinario()
        {
            await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Bolt", _outroVeterinario.Id));

            var pets = new PetRepository(Contexto);

            (await pets.ContarAtivos(Clinica.Id)).Should().Be(2);
            (await pets.ContarAtivos(Clinica.Id, Veterinario.Id)).Should().Be(1);
            (await pets.ContarAtivos(Clinica.Id, _outroVeterinario.Id)).Should().Be(1);
        }
    }
}
