using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
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
    /// Validação dos dados de cadastro e de edição de cada perfil (Administrador, Veterinário,
    /// Apoio e Tutor), o perfil Tutor fora da tela de Usuários e a clínica que atende só cães.
    /// </summary>
    public class ValidacaoDeCadastroTests : BaseDeTeste
    {
        private readonly FilaDeEmails _fila = new();

        private GerenciarUsuariosUseCase CasoDeUsuarios(UsuarioAtual usuarioAtual) => new(
            new UsuarioRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new TutorRepository(Contexto),
            new ApoioRepository(Contexto),
            new PasswordHasher(),
            Dependencias.Contas(Contexto, _fila),
            new ContasAtivas(new MemoryCache(new MemoryCacheOptions())),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private GerenciarTutoresUseCase CasoDeTutores(UsuarioAtual usuarioAtual) => new(
            new TutorRepository(Contexto),
            new UsuarioRepository(Contexto),
            new PasswordHasher(),
            Dependencias.Contas(Contexto, _fila),
            Dependencias.Auditoria(Contexto, usuarioAtual),
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

        // ------------------------------------------------------------------ regras de formato

        [Theory]
        [InlineData("Ana Souza")]
        [InlineData("Dra. Helena Duarte")]
        [InlineData("Maria da Silva D'Ávila")]
        [InlineData("João Pedro Gonçalves-Lima")]
        public void Nome_completo_com_letras_e_aceito(string nome) =>
            ValidacaoDeCadastro.Nome(nome).Should().BeNull();

        [Theory]
        [InlineData("Ana", "nome e sobrenome")]
        [InlineData("Dr. Carlos", "nome e sobrenome")]
        [InlineData("Ana Souza 2", "sem números")]
        [InlineData("Ana@Souza", "sem números")]
        [InlineData("  ", "entre 3 e 120")]
        public void Nome_incompleto_ou_com_numeros_e_recusado(string nome, string trecho) =>
            ValidacaoDeCadastro.Nome(nome).Should().Contain(trecho);

        [Theory]
        [InlineData("ana@clinica.com.br", true)]
        [InlineData("a.b+c@vet-care.com", true)]
        [InlineData("ana@clinica", false)]
        [InlineData("ana.clinica.com", false)]
        [InlineData("ana@@clinica.com", false)]
        [InlineData("ana..souza@clinica.com", false)]
        public void Email_precisa_de_dominio_com_extensao(string email, bool valido) =>
            (ValidacaoDeCadastro.Email(email) == null).Should().Be(valido);

        [Theory]
        [InlineData("CRMV-DF 12345", "CRMV-DF 12345")]
        [InlineData("crmv df 12345", "CRMV-DF 12345")]
        [InlineData("SP-4321", "CRMV-SP 4321")]
        [InlineData("98765/RJ", "CRMV-RJ 98765")]
        public void Crmv_e_padronizado(string informado, string esperado) =>
            ValidacaoDeCadastro.Crmv(informado).Valor.Should().Be(esperado);

        [Theory]
        [InlineData("CRMV-XX 12345")]
        [InlineData("12345")]
        [InlineData("CRMV-DF")]
        [InlineData("")]
        public void Crmv_sem_uf_valida_ou_sem_numero_e_recusado(string crmv) =>
            ValidacaoDeCadastro.Crmv(crmv).Erro.Should().NotBeNull();

        [Theory]
        [InlineData("61999990000", "(61) 99999-0000")]
        [InlineData("(61) 3333-4444", "(61) 3333-4444")]
        [InlineData("+55 11 98765-4321", "(11) 98765-4321")]
        public void Telefone_com_ddd_e_padronizado(string informado, string esperado) =>
            ValidacaoDeCadastro.Telefone(informado, obrigatorio: true).Valor.Should().Be(esperado);

        [Theory]
        [InlineData("9999-0000")]
        [InlineData("(61) 89999-0000")]
        [InlineData("(01) 99999-0000")]
        [InlineData("")]
        public void Telefone_sem_ddd_ou_celular_sem_nove_e_recusado(string telefone) =>
            ValidacaoDeCadastro.Telefone(telefone, obrigatorio: true).Erro.Should().NotBeNull();

        [Fact]
        public void Cpf_confere_os_digitos_verificadores()
        {
            ValidacaoDeCadastro.Cpf("12345678909").Valor.Should().Be("123.456.789-09");
            ValidacaoDeCadastro.Cpf("123.456.789-00").Erro.Should().NotBeNull();
            ValidacaoDeCadastro.Cpf("111.111.111-11").Erro.Should().NotBeNull();
            ValidacaoDeCadastro.Cpf("").Valor.Should().BeEmpty("o CPF é opcional");
        }

        [Theory]
        [InlineData("vetcare123", true)]
        [InlineData("Senha2026", true)]
        [InlineData("abc123", false)]
        [InlineData("somenteletras", false)]
        [InlineData("1234567890", false)]
        public void Senha_tem_oito_caracteres_com_letras_e_numeros(string senha, bool aceita) =>
            (PasswordHasher.ValidarForca(senha) == null).Should().Be(aceita);

        // ------------------------------------------------------------------ usuários da equipe

        [Fact]
        public async Task Tela_de_usuarios_nao_cadastra_tutor()
        {
            var resultado = await CasoDeUsuarios(ComoAdministrador()).Cadastrar(new CriarUsuarioDTO
            {
                Nome = "Paula Rocha",
                Email = "paula@teste.com",
                Perfil = Perfis.Tutor
            }, Clinica.Id);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("tela de Tutores");
            Contexto.Usuarios.Should().NotContain(u => u.Email == "paula@teste.com");
        }

        [Fact]
        public async Task Veterinario_e_cadastrado_com_o_crmv_padronizado()
        {
            var resultado = await CasoDeUsuarios(ComoAdministrador()).Cadastrar(new CriarUsuarioDTO
            {
                Nome = "Dr. Rafael Moura",
                Email = "rafael@teste.com",
                Perfil = Perfis.Veterinario,
                Crmv = "go 4567"
            }, Clinica.Id);

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Crmv.Should().Be("CRMV-GO 4567");
        }

        [Fact]
        public async Task Crmv_repetido_e_recusado_mesmo_escrito_de_outro_jeito()
        {
            // O veterinário base tem o CRMV gravado no formato padrão.
            var crmvDoBase = Contexto.Veterinarios.Single(v => v.Id == Veterinario.Id).Crmv;
            var digitado = crmvDoBase.Replace("CRMV-", string.Empty).ToLowerInvariant();

            var resultado = await CasoDeUsuarios(ComoAdministrador()).Cadastrar(new CriarUsuarioDTO
            {
                Nome = "Dra. Bianca Prado",
                Email = "bianca@teste.com",
                Perfil = Perfis.Veterinario,
                Crmv = digitado
            }, Clinica.Id);

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("CRMV");
        }

        [Fact]
        public async Task Edicao_valida_o_novo_nome_e_mantem_um_cadastro_antigo_editavel()
        {
            // "Administradora" tem uma palavra só: o cadastro antigo continua editável sem trocar o nome.
            var mesmoNome = await CasoDeUsuarios(ComoAdministrador()).Atualizar(UsuarioAdministrador.Id, new AtualizarUsuarioDTO
            {
                Nome = UsuarioAdministrador.Nome,
                Email = "admin.nova@teste.com"
            });

            var nomeIncompleto = await CasoDeUsuarios(ComoAdministrador()).Atualizar(UsuarioVeterinario.Id, new AtualizarUsuarioDTO
            {
                Nome = "Helena",
                Email = UsuarioVeterinario.Email
            });

            mesmoNome.Sucesso.Should().BeTrue(mesmoNome.Mensagem);
            nomeIncompleto.Sucesso.Should().BeFalse();
            nomeIncompleto.Mensagem.Should().Contain("nome e sobrenome");
        }

        // ------------------------------------------------------------------ tutores

        [Fact]
        public async Task Tutor_precisa_de_telefone_e_tem_contato_padronizado()
        {
            var semTelefone = await CasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Paula Rocha",
                Email = "paula@teste.com"
            });

            var completo = await CasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Paula Rocha",
                Email = "paula@teste.com",
                Telefone = "61 98123 4567",
                Cpf = "12345678909",
                Endereco = "QS 1, Rua 210, Águas Claras"
            });

            semTelefone.Sucesso.Should().BeFalse();
            semTelefone.Mensagem.Should().Contain("telefone");
            completo.Sucesso.Should().BeTrue(completo.Mensagem);
            completo.Dados!.Telefone.Should().Be("(61) 98123-4567");
            completo.Dados.Cpf.Should().Be("123.456.789-09");
        }

        [Fact]
        public async Task Cpf_de_outro_tutor_e_recusado()
        {
            await CasoDeTutores(ComoAdministrador()).Atualizar(Tutor.Id, new AtualizarTutorDTO { Cpf = "123.456.789-09" });

            var repetido = await CasoDeTutores(ComoAdministrador()).Cadastrar(new CriarTutorDTO
            {
                Nome = "Paula Rocha",
                Email = "paula@teste.com",
                Telefone = "(61) 98123-4567",
                Cpf = "12345678909"
            });

            repetido.Sucesso.Should().BeFalse();
            repetido.Mensagem.Should().Contain("CPF");
        }

        [Fact]
        public async Task Equipe_edita_nome_e_email_do_tutor_mas_o_proprio_tutor_nao()
        {
            var pelaEquipe = await CasoDeTutores(ComoAdministrador()).Atualizar(Tutor.Id, new AtualizarTutorDTO
            {
                Nome = "Tutor Renomeado Silva",
                Email = "tutor.novo@teste.com"
            });

            var peloTutor = await CasoDeTutores(ComoTutor()).Atualizar(Tutor.Id, new AtualizarTutorDTO
            {
                Nome = "Outro Nome Qualquer"
            });

            var contatoPeloTutor = await CasoDeTutores(ComoTutor()).Atualizar(Tutor.Id, new AtualizarTutorDTO
            {
                Telefone = "(61) 3333-4444"
            });

            pelaEquipe.Sucesso.Should().BeTrue(pelaEquipe.Mensagem);
            pelaEquipe.Dados!.Nome.Should().Be("Tutor Renomeado Silva");
            pelaEquipe.Dados.Email.Should().Be("tutor.novo@teste.com");
            peloTutor.Falha.Should().Be(TipoFalha.NaoAutorizado);
            contatoPeloTutor.Sucesso.Should().BeTrue(contatoPeloTutor.Mensagem);
        }

        [Fact]
        public async Task Telefone_do_tutor_nao_pode_ser_apagado_na_edicao()
        {
            var resultado = await CasoDeTutores(ComoAdministrador()).Atualizar(Tutor.Id, new AtualizarTutorDTO { Telefone = "" });

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("telefone");
        }

        // ------------------------------------------------------------------ só cães

        private CriarPetDTO PacienteDaEspecie(string especie) => new()
        {
            Nome = "Paçoca",
            Especie = especie,
            Raca = "SRD",
            Sexo = "Macho",
            DataNascimento = new DateTime(2022, 1, 10),
            TutorId = Tutor.Id,
            VeterinarioResponsavelId = Veterinario.Id
        };

        [Fact]
        public async Task Clinica_cadastra_apenas_caes()
        {
            var gato = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteDaEspecie("Gato"));
            var cao = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteDaEspecie("Cão"));
            var semEspecie = await CasoDePacientes(ComoAdministrador()).Cadastrar(PacienteDaEspecie(""));

            gato.Sucesso.Should().BeFalse();
            gato.Mensagem.Should().Contain("apenas cães");
            cao.Dados!.Especie.Should().Be("Cachorro");
            semEspecie.Dados!.Especie.Should().Be("Cachorro");
        }

        [Fact]
        public async Task Paciente_antigo_de_outra_especie_continua_editavel_sem_trocar_de_especie()
        {
            var gato = new Pet
            {
                ClinicaId = Clinica.Id,
                TutorId = Tutor.Id,
                VeterinarioResponsavelId = Veterinario.Id,
                Nome = "Mimi",
                Especie = "Gato",
                DataNascimento = DateTime.SpecifyKind(new DateTime(2020, 5, 1), DateTimeKind.Utc)
            };

            Contexto.Pets.Add(gato);
            Contexto.SaveChanges();

            var edicao = new AtualizarPetDTO
            {
                Nome = "Mimi",
                Especie = "Gato",
                DataNascimento = gato.DataNascimento,
                TutorId = Tutor.Id,
                PesoAtualKg = 4.1m
            };

            var mantida = await CasoDePacientes(ComoAdministrador()).Atualizar(gato.Id, edicao);

            edicao.Especie = "Ave";
            var trocada = await CasoDePacientes(ComoAdministrador()).Atualizar(gato.Id, edicao);

            mantida.Sucesso.Should().BeTrue(mantida.Mensagem);
            trocada.Sucesso.Should().BeFalse();
        }
    }
}
