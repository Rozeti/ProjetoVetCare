using FluentAssertions;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// HU-003 estendida: o tutor cadastra sozinho o animal recém-adquirido. O ponto
    /// sensível é o vínculo da RN-001 — ele tem de sair do token de quem está agindo,
    /// nunca de um campo enviado pelo cliente.
    /// </summary>
    public class CadastroPeloTutorTests : BaseDeTeste
    {
        private GerenciarPacientesUseCase CriarCasoDeUso(UsuarioAtual usuarioAtual) => new(
            new PetRepository(Contexto),
            new TutorRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new AlergiaRepository(Contexto),
            new VacinaRepository(Contexto),
            new TratamentoRepository(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private static CriarPetDoTutorDTO PetNovo() => new()
        {
            Nome = "Nina",
            Especie = "Gato",
            Raca = "SRD",
            Sexo = "Fêmea",
            DataNascimento = new DateTime(2024, 3, 12),
            PesoAtualKg = 3.4m
        };

        [Fact]
        public async Task Tutor_cadastra_o_proprio_pet()
        {
            var resultado = await CriarCasoDeUso(ComoTutor()).CadastrarComoTutor(PetNovo());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.TutorId.Should().Be(Tutor.Id);
            resultado.Dados.Nome.Should().Be("Nina");
        }

        [Fact]
        public async Task Pet_cadastrado_pelo_tutor_ja_nasce_com_prontuario()
        {
            var resultado = await CriarCasoDeUso(ComoTutor()).CadastrarComoTutor(PetNovo());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            Contexto.Prontuarios
                .FirstOrDefault(p => p.PacienteId == resultado.Dados!.Id)
                .Should().NotBeNull("o histórico clínico precisa existir desde o primeiro dia");
        }

        [Fact]
        public async Task Pet_cadastrado_pelo_tutor_fica_visivel_para_a_clinica()
        {
            var cadastro = await CriarCasoDeUso(ComoTutor()).CadastrarComoTutor(PetNovo());
            cadastro.Sucesso.Should().BeTrue(cadastro.Mensagem);

            var listagem = await CriarCasoDeUso(ComoAdministrador())
                .Listar(null, "Nina", true, new API.Common.ParametrosPagina());

            listagem.Sucesso.Should().BeTrue();
            listagem.Dados!.Itens.Should().ContainSingle(p => p.Id == cadastro.Dados!.Id);
        }

        /// <summary>
        /// RN-001 e HU-013, CA-1: mesmo que outro tutor exista na clínica, o pet nasce
        /// vinculado a quem cadastrou — o cliente não tem como escolher o responsável.
        /// </summary>
        [Fact]
        public async Task Tutor_nao_cadastra_pet_no_nome_de_outro()
        {
            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Outro Tutor",
                Email = "outro@teste.com",
                Perfil = Perfis.Tutor,
                SenhaHash = "hash",
                Ativo = true
            };

            var outroTutor = new Tutor { UsuarioId = outroUsuario.Id, Telefone = "(61) 91111-1111" };

            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Tutores.Add(outroTutor);
            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoTutor()).CadastrarComoTutor(PetNovo());

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.TutorId.Should().Be(Tutor.Id);
            resultado.Dados.TutorId.Should().NotBe(outroTutor.Id);
        }

        [Fact]
        public async Task Equipe_da_clinica_nao_usa_o_cadastro_do_tutor()
        {
            var resultado = await CriarCasoDeUso(ComoAdministrador()).CadastrarComoTutor(PetNovo());

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(API.Common.TipoFalha.NaoAutorizado);
        }

        [Fact]
        public async Task Usuario_tutor_sem_cadastro_de_tutor_recebe_orientacao()
        {
            var semVinculo = ComoUsuario(UsuarioTutor, Clinica.Id);

            var resultado = await CriarCasoDeUso(semVinculo).CadastrarComoTutor(PetNovo());

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("clínica");
        }

        [Fact]
        public async Task Data_de_nascimento_futura_continua_recusada_no_cadastro_do_tutor()
        {
            var dto = PetNovo();
            dto.DataNascimento = DateTime.UtcNow.AddDays(5);

            var resultado = await CriarCasoDeUso(ComoTutor()).CadastrarComoTutor(dto);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("futura");
        }

        [Fact]
        public async Task Microchip_duplicado_continua_recusado_no_cadastro_do_tutor()
        {
            var casoDeUso = CriarCasoDeUso(ComoTutor());

            var primeiro = PetNovo();
            primeiro.Microchip = "982000123456789";

            (await casoDeUso.CadastrarComoTutor(primeiro)).Sucesso.Should().BeTrue();

            var segundo = PetNovo();
            segundo.Nome = "Bidu";
            segundo.Microchip = "982000123456789";

            var resultado = await casoDeUso.CadastrarComoTutor(segundo);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("microchip");
        }
    }
}
