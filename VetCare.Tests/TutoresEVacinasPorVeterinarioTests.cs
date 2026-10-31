using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services.Email;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// Todo veterinário alcança todos os pacientes da clínica, e com eles todos os tutores e
    /// todas as carteiras de vacinação: se o responsável faltar, um colega atende o animal sem
    /// precisar de transferência.
    /// </summary>
    public class TutoresEVacinasPorVeterinarioTests : BaseDeTeste
    {
        private readonly Usuario _usuarioOutroVeterinario;
        private readonly Veterinario _outroVeterinario;
        private readonly Usuario _usuarioOutroTutor;
        private readonly Tutor _outroTutor;

        public TutoresEVacinasPorVeterinarioTests()
        {
            _usuarioOutroVeterinario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Dr. Outro Veterinário",
                Email = "outro.vet@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };

            _outroVeterinario = new Veterinario
            {
                UsuarioId = _usuarioOutroVeterinario.Id,
                Crmv = "CRMV-DF 2000",
                Especialidade = "Ortopedia"
            };

            _usuarioOutroTutor = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Tutora do Outro",
                Email = "outra.tutora@teste.com",
                Perfil = Perfis.Tutor,
                SenhaHash = "hash"
            };

            _outroTutor = new Tutor { UsuarioId = _usuarioOutroTutor.Id, Telefone = "(61) 91111-1111" };

            Contexto.Usuarios.AddRange(_usuarioOutroVeterinario, _usuarioOutroTutor);
            Contexto.Veterinarios.Add(_outroVeterinario);
            Contexto.Tutores.Add(_outroTutor);

            // O paciente da outra tutora é acompanhado pelo outro veterinário.
            Contexto.Pets.Add(new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = _outroTutor.Id,
                VeterinarioResponsavelId = _outroVeterinario.Id,
                Nome = "Bolt",
                Especie = "Cachorro",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2021, 1, 1), DateTimeKind.Utc)
            });

            Contexto.SaveChanges();
        }

        private UsuarioAtual ComoOutroVeterinario() =>
            ComoUsuario(_usuarioOutroVeterinario, Clinica.Id, veterinarioId: _outroVeterinario.Id);

        private GerenciarTutoresUseCase CasoDeTutores(UsuarioAtual usuarioAtual) => new(
            new TutorRepository(Contexto),
            new UsuarioRepository(Contexto),
            new PasswordHasher(),
            Dependencias.Contas(Contexto, new FilaDeEmails()),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarVacinasUseCase CasoDeVacinas(UsuarioAtual usuarioAtual) => new(
            new VacinaRepository(Contexto),
            new PetRepository(Contexto),
            new VeterinarioRepository(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private static ParametrosPagina Pagina() => new() { Pagina = 1, Tamanho = 50 };

        private async Task<Vacina> VacinaDoPacienteBase()
        {
            var vacina = new Vacina
            {
                PacienteId = Paciente.Id,
                VeterinarioId = Veterinario.Id,
                Nome = "V10",
                DataAplicacao = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-10), DateTimeKind.Utc)
            };

            Contexto.Vacinas.Add(vacina);
            await Contexto.SaveChangesAsync();

            return vacina;
        }

        [Fact]
        public async Task Todo_veterinario_lista_todos_os_tutores_da_clinica()
        {
            var doPrimeiro = await CasoDeTutores(ComoVeterinario()).Listar(null, Pagina());
            var doSegundo = await CasoDeTutores(ComoOutroVeterinario()).Listar(null, Pagina());
            var daClinica = await CasoDeTutores(ComoAdministrador()).Listar(null, Pagina());

            var esperados = new[] { UsuarioTutor.Nome, _usuarioOutroTutor.Nome };

            doPrimeiro.Dados!.Itens.Select(t => t.Nome).Should().BeEquivalentTo(esperados);
            doSegundo.Dados!.Itens.Select(t => t.Nome).Should().BeEquivalentTo(esperados);
            daClinica.Dados!.Itens.Should().HaveCount(2);
        }

        [Fact]
        public async Task Lista_de_tutores_mostra_todos_os_animais_vinculados()
        {
            // A outra tutora também leva um pet ao primeiro veterinário.
            Contexto.Pets.Add(new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = _outroTutor.Id,
                VeterinarioResponsavelId = Veterinario.Id,
                Nome = "Amora",
                Especie = "Gato",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2022, 3, 1), DateTimeKind.Utc)
            });
            Contexto.SaveChanges();

            var daClinica = await CasoDeTutores(ComoAdministrador()).Listar(null, Pagina());
            var doSegundo = await CasoDeTutores(ComoOutroVeterinario()).Listar(null, Pagina());

            daClinica.Dados!.Itens.Single(t => t.Id == _outroTutor.Id).Pets.Select(p => p.Nome)
                .Should().Equal("Amora", "Bolt");

            // O outro veterinário vê os dois animais, inclusive o que a colega acompanha.
            doSegundo.Dados!.Itens.Single(t => t.Id == _outroTutor.Id).Pets.Select(p => p.Nome)
                .Should().Equal("Amora", "Bolt");
        }

        [Fact]
        public async Task Selecao_de_tutores_traz_todos_os_tutores_para_qualquer_veterinario()
        {
            var usuarioNovo = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Tutor Recém-chegado",
                Email = "novo.tutor@teste.com",
                Perfil = Perfis.Tutor,
                SenhaHash = "hash"
            };

            Contexto.Usuarios.Add(usuarioNovo);
            Contexto.Tutores.Add(new Tutor { UsuarioId = usuarioNovo.Id });
            await Contexto.SaveChangesAsync();

            var selecao = await CasoDeTutores(ComoVeterinario()).ListarParaSelecao();

            selecao.Dados!.Select(t => t.Nome).Should()
                .Contain(new[] { "Tutor Recém-chegado", _usuarioOutroTutor.Nome, UsuarioTutor.Nome });
        }

        /// <summary>
        /// RN-014: o veterinário alcança a ficha de qualquer tutor para atender o animal, mas
        /// não a mantém — o cadastro é do administrador e do assistente administrativo.
        /// </summary>
        [Fact]
        public async Task Qualquer_veterinario_abre_o_tutor_mas_nao_altera_o_cadastro()
        {
            var leitura = await CasoDeTutores(ComoOutroVeterinario()).ObterPorId(Tutor.Id);
            var alteracao = await CasoDeTutores(ComoOutroVeterinario())
                .Atualizar(Tutor.Id, new AtualizarTutorDTO { Telefone = "(61) 90000-9999" });

            leitura.Sucesso.Should().BeTrue(leitura.Mensagem);
            alteracao.Sucesso.Should().BeFalse();
            alteracao.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }

        /// <summary>O administrador continua mantendo o contato do tutor.</summary>
        [Fact]
        public async Task Administrador_atualiza_o_contato_do_tutor()
        {
            var alteracao = await CasoDeTutores(ComoAdministrador())
                .Atualizar(Tutor.Id, new AtualizarTutorDTO { Telefone = "(61) 90000-9999" });

            alteracao.Sucesso.Should().BeTrue(alteracao.Mensagem);
            alteracao.Dados!.Telefone.Should().Be("(61) 90000-9999");
        }

        [Fact]
        public async Task Contagem_de_pacientes_do_tutor_inclui_todos_os_animais()
        {
            // Segundo pet do tutor base, acompanhado pelo outro veterinário.
            Contexto.Pets.Add(new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = Tutor.Id,
                VeterinarioResponsavelId = _outroVeterinario.Id,
                Nome = "Mel",
                Especie = "Gato",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2022, 1, 1), DateTimeKind.Utc)
            });
            await Contexto.SaveChangesAsync();

            var doPrimeiro = await CasoDeTutores(ComoVeterinario()).ObterPorId(Tutor.Id);
            var daClinica = await CasoDeTutores(ComoAdministrador()).ObterPorId(Tutor.Id);

            doPrimeiro.Dados!.QuantidadePets.Should().Be(2);
            daClinica.Dados!.QuantidadePets.Should().Be(2);
        }

        [Fact]
        public async Task Veterinario_responsavel_corrige_e_apaga_aplicacoes_do_seu_paciente()
        {
            var vacina = await VacinaDoPacienteBase();

            var correcao = await CasoDeVacinas(ComoVeterinario()).Atualizar(vacina.Id, new AtualizarVacinaDTO
            {
                Tipo = "Vacina",
                Nome = "V10 (lote corrigido)",
                Lote = "L-2026",
                DataAplicacao = vacina.DataAplicacao
            });

            correcao.Sucesso.Should().BeTrue(correcao.Mensagem);
            correcao.Dados!.Nome.Should().Be("V10 (lote corrigido)");

            var exclusao = await CasoDeVacinas(ComoVeterinario()).Remover(vacina.Id, "Lançada no paciente errado");

            exclusao.Sucesso.Should().BeTrue(exclusao.Mensagem);
            Contexto.Vacinas.Should().NotContain(v => v.Id == vacina.Id);
            Contexto.RegistrosAuditoria.Should().Contain(r => r.Entidade == "Vacina" && r.Acao == "Exclusao");
        }

        [Fact]
        public async Task Veterinario_que_cobre_a_colega_corrige_a_carteira_do_paciente()
        {
            var vacina = await VacinaDoPacienteBase();

            var correcao = await CasoDeVacinas(ComoOutroVeterinario()).Atualizar(vacina.Id, new AtualizarVacinaDTO
            {
                Tipo = "Vacina",
                Nome = "V10 (fabricante corrigido)",
                DataAplicacao = vacina.DataAplicacao
            });

            correcao.Sucesso.Should().BeTrue(correcao.Mensagem);
            correcao.Dados!.Nome.Should().Be("V10 (fabricante corrigido)");
        }
    }
}
