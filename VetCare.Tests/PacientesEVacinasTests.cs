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
    /// <summary>HU-003 e prevenção: cadastro de pacientes, alertas clínicos e carteira de vacinação.</summary>
    public class PacientesEVacinasTests : BaseDeTeste
    {
        private GerenciarPacientesUseCase CriarCasoDePacientes(UsuarioAtual usuarioAtual) => new(
            new PetRepository(Contexto),
            new TutorRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new AlergiaRepository(Contexto),
            new VacinaRepository(Contexto),
            new TratamentoRepository(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarVacinasUseCase CriarCasoDeVacinas(UsuarioAtual usuarioAtual) => new(
            new VacinaRepository(Contexto),
            new PetRepository(Contexto),
            usuarioAtual);

        private CriarPetDTO PacienteNovo() => new()
        {
            Nome = "Mel",
            Especie = "Cachorro",
            Raca = "Beagle",
            Sexo = "Fêmea",
            DataNascimento = new DateTime(2021, 5, 20),
            TutorId = Tutor.Id
        };

        [Fact]
        public async Task Paciente_sem_tutor_e_recusado()
        {
            var dto = PacienteNovo();
            dto.TutorId = Guid.Empty;

            var resultado = await CriarCasoDePacientes(ComoAdministrador()).Cadastrar(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("tutor");
        }

        [Fact]
        public async Task Paciente_cadastrado_ja_nasce_com_prontuario()
        {
            var resultado = await CriarCasoDePacientes(ComoAdministrador()).Cadastrar(PacienteNovo());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var prontuario = Contexto.Prontuarios.FirstOrDefault(p => p.PacienteId == resultado.Dados!.Id);
            prontuario.Should().NotBeNull();
        }

        [Fact]
        public async Task Data_de_nascimento_futura_e_recusada()
        {
            var dto = PacienteNovo();
            dto.DataNascimento = DateTime.UtcNow.AddDays(10);

            var resultado = await CriarCasoDePacientes(ComoAdministrador()).Cadastrar(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("futura");
        }

        [Fact]
        public async Task Microchip_nao_se_repete_na_mesma_clinica()
        {
            var casoDeUso = CriarCasoDePacientes(ComoAdministrador());

            var primeiro = PacienteNovo();
            primeiro.Microchip = "982000123456789";
            (await casoDeUso.Cadastrar(primeiro)).Sucesso.Should().BeTrue();

            var segundo = PacienteNovo();
            segundo.Nome = "Outro";
            segundo.Microchip = "982000123456789";

            var resultado = await casoDeUso.Cadastrar(segundo);

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Paciente_com_historico_nao_pode_ser_excluido()
        {
            Contexto.AvaliacoesClinicas.Add(new AvaliacaoClinica
            {
                TratamentoId = Tratamento.Id,
                ProntuarioId = Prontuario.Id,
                VeterinarioId = Veterinario.Id,
                QueixaPrincipal = "q",
                Anamnese = "a",
                ExameFisico = "e",
                HipoteseDiagnostica = "h",
                PlanoTerapeutico = "p"
            });

            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDePacientes(ComoAdministrador()).Excluir(Paciente.Id);

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            resultado.Mensagem.Should().Contain("inativação");
        }

        [Fact]
        public async Task Registro_de_obito_inativa_o_paciente_e_encerra_o_tratamento()
        {
            var resultado = await CriarCasoDePacientes(ComoAdministrador()).Atualizar(Paciente.Id, new AtualizarPetDTO
            {
                Nome = Paciente.Nome,
                Especie = Paciente.Especie,
                Raca = Paciente.Raca,
                DataNascimento = Paciente.DataNascimento,
                TutorId = Tutor.Id,
                DataObito = DateTime.UtcNow.Date
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var pet = await Contexto.Pets.FindAsync(Paciente.Id);
            pet!.Ativo.Should().BeFalse();

            var tratamento = await Contexto.Tratamentos.FindAsync(Tratamento.Id);
            tratamento!.Status.Should().Be("Interrompido");
            tratamento.DataFim.Should().NotBeNull();
        }

        [Fact]
        public void Idade_de_filhote_e_descrita_em_meses()
        {
            var tresMeses = DateTime.UtcNow.Date.AddMonths(-3);

            GerenciarPacientesUseCase.DescreverIdade(tresMeses).Should().Be("3 meses");
            GerenciarPacientesUseCase.CalcularIdade(tresMeses).Should().Be(0);
        }

        [Fact]
        public void Idade_de_adulto_e_descrita_em_anos()
        {
            var doisAnos = DateTime.UtcNow.Date.AddYears(-2).AddDays(-1);

            GerenciarPacientesUseCase.DescreverIdade(doisAnos).Should().Be("2 anos");
        }

        [Fact]
        public async Task Proxima_dose_anterior_a_aplicacao_e_recusada()
        {
            var resultado = await CriarCasoDeVacinas(ComoVeterinario()).Registrar(new CriarVacinaDTO
            {
                PacienteId = Paciente.Id,
                Tipo = "Vacina",
                Nome = "V10",
                DataAplicacao = DateTime.UtcNow.Date.AddDays(-10),
                ProximaDose = DateTime.UtcNow.Date.AddDays(-20)
            });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("posterior");
        }

        [Theory]
        [InlineData(-5, "Vencida")]
        [InlineData(10, "A vencer")]
        [InlineData(120, "Em dia")]
        public async Task Situacao_da_dose_reflete_o_prazo(int diasAteProximaDose, string situacaoEsperada)
        {
            var casoDeUso = CriarCasoDeVacinas(ComoVeterinario());

            await casoDeUso.Registrar(new CriarVacinaDTO
            {
                PacienteId = Paciente.Id,
                Tipo = "Vacina",
                Nome = "V10",
                DataAplicacao = DateTime.UtcNow.Date.AddDays(-200),
                ProximaDose = DateTime.UtcNow.Date.AddDays(diasAteProximaDose)
            });

            var lista = await casoDeUso.ListarPorPaciente(Paciente.Id);

            lista.Dados!.Single().SituacaoDose.Should().Be(situacaoEsperada);
        }

        [Fact]
        public async Task Dose_unica_nao_entra_no_controle_de_vencimento()
        {
            var casoDeUso = CriarCasoDeVacinas(ComoVeterinario());

            await casoDeUso.Registrar(new CriarVacinaDTO
            {
                PacienteId = Paciente.Id,
                Tipo = "Vermifugo",
                Nome = "Dose avulsa",
                DataAplicacao = DateTime.UtcNow.Date.AddDays(-30)
            });

            var lista = await casoDeUso.ListarPorPaciente(Paciente.Id);

            lista.Dados!.Single().SituacaoDose.Should().Be("Dose única");
            lista.Dados.Single().DiasParaProximaDose.Should().BeNull();
        }

        [Fact]
        public async Task Tutor_nao_acessa_a_carteira_de_pet_de_outro_tutor()
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

            var comoOutro = ComoUsuario(outroUsuario, Clinica.Id, tutorId: outroTutor.Id);

            var resultado = await CriarCasoDeVacinas(comoOutro).ListarPorPaciente(Paciente.Id);

            resultado.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }
    }
}
