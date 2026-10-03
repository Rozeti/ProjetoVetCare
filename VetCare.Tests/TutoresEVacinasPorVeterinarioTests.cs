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
    /// O tutor acompanha o paciente: o veterinário vê os tutores dos seus pacientes (e os
    /// que ainda não têm paciente), e a transferência leva o tutor junto. A carteira de
    /// vacinação segue a mesma regra: só o responsável e a administração corrigem ou
    /// apagam aplicações.
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
            usuarioAtual);

        private GerenciarVacinasUseCase CasoDeVacinas(UsuarioAtual usuarioAtual) => new(
            new VacinaRepository(Contexto),
            new PetRepository(Contexto),
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
        public async Task Veterinario_lista_apenas_os_tutores_dos_seus_pacientes()
        {
            var doPrimeiro = await CasoDeTutores(ComoVeterinario()).Listar(null, Pagina());
            var doSegundo = await CasoDeTutores(ComoOutroVeterinario()).Listar(null, Pagina());
            var daClinica = await CasoDeTutores(ComoAdministrador()).Listar(null, Pagina());

            doPrimeiro.Dados!.Itens.Select(t => t.Nome).Should().BeEquivalentTo(new[] { UsuarioTutor.Nome });
            doSegundo.Dados!.Itens.Select(t => t.Nome).Should().BeEquivalentTo(new[] { _usuarioOutroTutor.Nome });
            daClinica.Dados!.Itens.Should().HaveCount(2);
        }

        [Fact]
        public async Task Tutor_sem_paciente_aparece_para_todos_os_veterinarios()
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

            selecao.Dados!.Select(t => t.Nome).Should().Contain("Tutor Recém-chegado",
                "sem ele o veterinário não conseguiria cadastrar o primeiro paciente do tutor");
            selecao.Dados.Select(t => t.Nome).Should().NotContain(_usuarioOutroTutor.Nome);
        }

        [Fact]
        public async Task Veterinario_nao_abre_nem_altera_tutor_de_outro_profissional()
        {
            var negado = await CasoDeTutores(ComoOutroVeterinario()).ObterPorId(Tutor.Id);
            var alteracao = await CasoDeTutores(ComoOutroVeterinario())
                .Atualizar(Tutor.Id, new AtualizarTutorDTO { Telefone = "(61) 90000-9999" });

            negado.Sucesso.Should().BeFalse();
            negado.Falha.Should().Be(TipoFalha.NaoEncontrado, "o cadastro nem deve parecer existir");
            alteracao.Falha.Should().Be(TipoFalha.NaoAutorizado);

            (await CasoDeTutores(ComoVeterinario()).ObterPorId(Tutor.Id)).Sucesso.Should().BeTrue();
        }

        [Fact]
        public async Task Contagem_de_pacientes_do_tutor_segue_o_recorte_do_veterinario()
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

            doPrimeiro.Dados!.QuantidadePets.Should().Be(1);
            daClinica.Dados!.QuantidadePets.Should().Be(2);
        }

        [Fact]
        public async Task Transferencia_do_paciente_leva_o_tutor_para_o_novo_veterinario()
        {
            var resultado = await CasoDeTransferencia(ComoAdministrador())
                .Executar(Paciente.Id, new TransferirPacienteDTO { VeterinarioId = _outroVeterinario.Id });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            Contexto.ChangeTracker.Clear();

            var doPrimeiro = await CasoDeTutores(ComoVeterinario()).Listar(null, Pagina());
            var doSegundo = await CasoDeTutores(ComoOutroVeterinario()).Listar(null, Pagina());

            doPrimeiro.Dados!.Itens.Should().BeEmpty("o único paciente do tutor foi embora");
            doSegundo.Dados!.Itens.Select(t => t.Nome).Should().Contain(UsuarioTutor.Nome);
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

            var exclusao = await CasoDeVacinas(ComoVeterinario()).Remover(vacina.Id);

            exclusao.Sucesso.Should().BeTrue(exclusao.Mensagem);
            Contexto.Vacinas.Should().NotContain(v => v.Id == vacina.Id);
            Contexto.RegistrosAuditoria.Should().Contain(r => r.Entidade == "Vacina" && r.Acao == "Exclusao");
        }

        [Fact]
        public async Task Outro_veterinario_nao_mexe_na_carteira_de_paciente_que_nao_e_seu()
        {
            var vacina = await VacinaDoPacienteBase();

            var correcao = await CasoDeVacinas(ComoOutroVeterinario()).Atualizar(vacina.Id, new AtualizarVacinaDTO
            {
                Tipo = "Vacina",
                Nome = "Outra",
                DataAplicacao = vacina.DataAplicacao
            });
            var exclusao = await CasoDeVacinas(ComoOutroVeterinario()).Remover(vacina.Id);

            correcao.Falha.Should().Be(TipoFalha.NaoAutorizado);
            exclusao.Falha.Should().Be(TipoFalha.NaoAutorizado);
            Contexto.Vacinas.Should().Contain(v => v.Id == vacina.Id);
        }
    }
}
