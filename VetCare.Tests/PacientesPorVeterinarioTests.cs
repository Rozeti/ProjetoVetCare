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
    /// Todo paciente tem um veterinário responsável, mas qualquer veterinário da clínica
    /// alcança qualquer paciente: se o responsável faltar, um colega atende o animal sem que a
    /// administração precise transferi-lo. O responsável é escolhido no cadastro e trocado na
    /// edição do cadastro.
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

        /// <summary>Avaliação de origem gravada direto no banco, para os cenários de tratamento.</summary>
        private AvaliacaoClinica AvaliacaoDe(Guid pacienteId, Guid veterinarioId)
        {
            var prontuario = Contexto.Prontuarios.FirstOrDefault(p => p.PacienteId == pacienteId);

            if (prontuario == null)
            {
                prontuario = new Prontuario { PacienteId = pacienteId };
                Contexto.Prontuarios.Add(prontuario);
            }

            var avaliacao = new AvaliacaoClinica
            {
                PacienteId = pacienteId,
                ProntuarioId = prontuario.Id,
                VeterinarioId = veterinarioId,
                MotivoConsulta = "Avaliação inicial",
                Observacoes = "Sem intercorrências",
                DiagnosticoPossivel = "A esclarecer"
            };

            Contexto.AvaliacoesClinicas.Add(avaliacao);
            Contexto.SaveChanges();

            return avaliacao;
        }

        private static CriarTratamentoDTO TratamentoNovo(Guid avaliacaoId, Guid? veterinarioId = null) => new()
        {
            AvaliacaoId = avaliacaoId,
            VeterinarioId = veterinarioId,
            Nome = "Fortalecimento",
            Tipo = "Fisioterapia",
            Descricao = "Fortalecimento muscular dos membros posteriores",
            DuracaoPrevista = "1 mês",
            Frequencia = "2 sessões por semana",
            SessoesPrevistas = 8
        };

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

        /// <summary>Paciente cadastrado pelo tutor no aplicativo: ninguém o acompanha ainda.</summary>
        private Pet PacienteSemResponsavel(string nome)
        {
            var pet = new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = Tutor.Id,
                Nome = nome,
                Especie = "Gato",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2023, 5, 1), DateTimeKind.Utc)
            };

            Contexto.Pets.Add(pet);
            Contexto.SaveChanges();

            return pet;
        }

        private AtualizarPetDTO EdicaoDe(Pet pet, Guid? veterinarioResponsavelId) => new()
        {
            Nome = pet.Nome,
            Especie = pet.Especie,
            Raca = pet.Raca,
            Sexo = pet.Sexo,
            DataNascimento = pet.DataNascimento,
            TutorId = pet.TutorId,
            VeterinarioResponsavelId = veterinarioResponsavelId
        };

        [Fact]
        public async Task Todo_veterinario_lista_todos_os_pacientes_da_clinica()
        {
            await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Bolt", _outroVeterinario.Id));

            var doPrimeiro = await CasoDePacientes(ComoVeterinario()).Listar(null, null, false, null, null, Pagina());
            var doSegundo = await CasoDePacientes(ComoOutroVeterinario()).Listar(null, null, false, null, null, Pagina());

            var todos = new[] { Paciente.Nome, "Bolt" };

            doPrimeiro.Dados!.Itens.Select(p => p.Nome).Should().BeEquivalentTo(todos);
            doSegundo.Dados!.Itens.Select(p => p.Nome).Should().BeEquivalentTo(todos);
        }

        [Fact]
        public async Task Veterinario_filtra_a_lista_pelo_responsavel()
        {
            await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Bolt", _outroVeterinario.Id));

            var meus = await CasoDePacientes(ComoVeterinario()).Listar(null, Veterinario.Id, false, null, null, Pagina());
            var daColega = await CasoDePacientes(ComoVeterinario()).Listar(null, _outroVeterinario.Id, false, null, null, Pagina());

            meus.Dados!.Itens.Select(p => p.Nome).Should().Equal(Paciente.Nome);
            daColega.Dados!.Itens.Select(p => p.Nome).Should().Equal("Bolt");
        }

        [Fact]
        public async Task Administracao_filtra_pacientes_sem_responsavel()
        {
            PacienteSemResponsavel("Sem Dono Clínico");

            var semResponsavel = await CasoDePacientes(ComoAdministrador()).Listar(null, null, true, null, null, Pagina());

            semResponsavel.Dados!.Itens.Should().ContainSingle(p => p.Nome == "Sem Dono Clínico");
            semResponsavel.Dados.Itens.Should().OnlyContain(p => p.VeterinarioResponsavelId == null);
        }

        [Fact]
        public async Task Qualquer_veterinario_abre_o_paciente_de_um_colega()
        {
            var colega = await CasoDePacientes(ComoOutroVeterinario()).ObterPorId(Paciente.Id);
            var responsavel = await CasoDePacientes(ComoVeterinario()).ObterPorId(Paciente.Id);

            colega.Sucesso.Should().BeTrue(colega.Mensagem);
            responsavel.Sucesso.Should().BeTrue(responsavel.Mensagem);
            colega.Dados!.VeterinarioResponsavelId.Should().Be(Veterinario.Id, "abrir o paciente não muda quem é o responsável");
        }

        [Fact]
        public async Task Veterinario_que_cadastra_e_o_responsavel_quando_nao_indica_outro()
        {
            var resultado = await CasoDePacientes(ComoVeterinario()).Cadastrar(PacienteNovo("Mel"));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioResponsavelId.Should().Be(Veterinario.Id);
            resultado.Dados.NomeVeterinarioResponsavel.Should().Be(UsuarioVeterinario.Nome);
        }

        [Fact]
        public async Task Veterinario_pode_cadastrar_paciente_para_um_colega()
        {
            var resultado = await CasoDePacientes(ComoVeterinario()).Cadastrar(PacienteNovo("Mel", _outroVeterinario.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
        }

        [Fact]
        public async Task Administracao_precisa_escolher_o_responsavel_no_cadastro()
        {
            var semResponsavel = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Mel"));
            var comResponsavel = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo("Mel", _outroVeterinario.Id));

            semResponsavel.Sucesso.Should().BeFalse();
            semResponsavel.Mensagem.Should().Contain("veterinário responsável");
            comResponsavel.Sucesso.Should().BeTrue(comResponsavel.Mensagem);
            comResponsavel.Dados!.VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
        }

        [Fact]
        public async Task Edicao_troca_o_responsavel_sem_mexer_nos_tratamentos()
        {
            var resultado = await CasoDePacientes(ComoAdministrador())
                .Atualizar(Paciente.Id, EdicaoDe(Paciente, _outroVeterinario.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);

            Contexto.ChangeTracker.Clear();
            Contexto.Pets.Single(p => p.Id == Paciente.Id).VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
            Contexto.Tratamentos.Single(t => t.Id == Tratamento.Id).VeterinarioId.Should().Be(Veterinario.Id,
                "o tratamento continua com quem o conduz");

            // O novo responsável e o tutor ficam sabendo; a troca fica na auditoria.
            Contexto.Notificacoes.Should().Contain(n => n.UsuarioId == _usuarioOutroVeterinario.Id);
            Contexto.Notificacoes.Should().Contain(n => n.UsuarioId == UsuarioTutor.Id && n.Titulo == "Novo veterinário responsável");
            Contexto.RegistrosAuditoria.Should().Contain(r =>
                r.Entidade == "Paciente" && r.Detalhe.Contains(_usuarioOutroVeterinario.Nome));
        }

        [Fact]
        public async Task Edicao_sem_responsavel_mantem_o_atual()
        {
            var resultado = await CasoDePacientes(ComoAdministrador()).Atualizar(Paciente.Id, EdicaoDe(Paciente, null));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioResponsavelId.Should().Be(Veterinario.Id);
        }

        [Fact]
        public async Task Paciente_sem_responsavel_precisa_ganhar_um_na_edicao()
        {
            var pet = PacienteSemResponsavel("Pipoca");

            var semEscolha = await CasoDePacientes(ComoAdministrador()).Atualizar(pet.Id, EdicaoDe(pet, null));
            var comEscolha = await CasoDePacientes(ComoAdministrador()).Atualizar(pet.Id, EdicaoDe(pet, _outroVeterinario.Id));

            semEscolha.Sucesso.Should().BeFalse();
            semEscolha.Mensagem.Should().Contain("veterinário responsável");
            comEscolha.Sucesso.Should().BeTrue(comEscolha.Mensagem);
            comEscolha.Dados!.VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
        }

        [Fact]
        public async Task Colega_abre_tratamento_para_paciente_de_outro_veterinario()
        {
            var resultado = await CasoDeTratamentos(ComoOutroVeterinario()).Cadastrar(TratamentoNovo(Avaliacao.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.VeterinarioId.Should().Be(_outroVeterinario.Id, "quem abre o tratamento é quem o conduz");

            Contexto.ChangeTracker.Clear();
            Contexto.Pets.Single(p => p.Id == Paciente.Id).VeterinarioResponsavelId.Should().Be(Veterinario.Id,
                "atender o paciente não troca o responsável");
        }

        [Fact]
        public async Task Primeiro_tratamento_define_o_responsavel_de_paciente_que_ainda_nao_tem()
        {
            var pet = PacienteSemResponsavel("Sem Dono Clínico");
            var avaliacao = AvaliacaoDe(pet.Id, _outroVeterinario.Id);

            var resultado = await CasoDeTratamentos(ComoOutroVeterinario()).Cadastrar(TratamentoNovo(avaliacao.Id));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            Contexto.ChangeTracker.Clear();
            Contexto.Pets.Single(p => p.Id == pet.Id).VeterinarioResponsavelId.Should().Be(_outroVeterinario.Id);
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
