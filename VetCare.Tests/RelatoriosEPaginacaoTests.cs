using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>HU-016, HU-017, RN-008 e a paginação exigida pelo RNF-004.</summary>
    public class RelatoriosEPaginacaoTests : BaseDeTeste
    {
        private GerarRelatorioProdutividadeUseCase CriarRelatorio(UsuarioAtual usuarioAtual) => new(
            new AtendimentoRepository(Contexto),
            new AvaliacaoRepository(Contexto),
            usuarioAtual);

        private async Task<Veterinario> CriarSegundoVeterinario()
        {
            var usuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Dr. Segundo",
                Email = "segundo@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };

            var veterinario = new Veterinario { UsuarioId = usuario.Id, Crmv = "CRMV-DF 3000" };

            Contexto.Usuarios.Add(usuario);
            Contexto.Veterinarios.Add(veterinario);
            await Contexto.SaveChangesAsync();

            return veterinario;
        }

        private async Task RegistrarAtendimento(Guid veterinarioId, string tecnicas, int escalaDor, DateTime quando)
        {
            var sessao = new Sessao
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = veterinarioId,
                DataHora = quando,
                Status = "Concluída"
            };

            Contexto.Sessoes.Add(sessao);

            Contexto.Atendimentos.Add(new AtendimentoFisioterapeutico
            {
                SessaoId = sessao.Id,
                TratamentoId = Tratamento.Id,
                ProntuarioId = Prontuario.Id,
                VeterinarioId = veterinarioId,
                TecnicasAplicadas = tecnicas,
                EscalaDor = escalaDor,
                EvolucaoClinica = "Registro de teste",
                DataRegistro = quando
            });

            await Contexto.SaveChangesAsync();
        }

        [Fact]
        public async Task Periodo_sem_registros_e_informado_sem_erro()
        {
            var resultado = await CriarRelatorio(ComoAdministrador())
                .Executar(new DateTime(2020, 1, 1), new DateTime(2020, 1, 31));

            resultado.Sucesso.Should().BeTrue();
            resultado.Dados!.SemRegistros.Should().BeTrue();
            resultado.Dados.TotalAtendimentos.Should().Be(0);
        }

        [Fact]
        public async Task Data_final_anterior_a_inicial_e_recusada()
        {
            var resultado = await CriarRelatorio(ComoAdministrador())
                .Executar(DateTime.Today, DateTime.Today.AddDays(-5));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("posterior");
        }

        [Fact]
        public async Task Tecnicas_sao_separadas_e_ranqueadas()
        {
            var ontem = DateTime.UtcNow.AddDays(-1);

            await RegistrarAtendimento(Veterinario.Id, "Hidroterapia, Laserterapia", 5, ontem);
            await RegistrarAtendimento(Veterinario.Id, "Hidroterapia; Cinesioterapia", 3, ontem);

            var resultado = await CriarRelatorio(ComoAdministrador())
                .Executar(DateTime.Today.AddDays(-7), DateTime.Today);

            var ranking = resultado.Dados!.TecnicasMaisAplicadas;

            ranking.Should().HaveCount(3);
            ranking[0].Tecnica.Should().Be("Hidroterapia");
            ranking[0].Ocorrencias.Should().Be(2);
        }

        [Fact]
        public async Task Media_da_escala_de_dor_e_calculada()
        {
            var ontem = DateTime.UtcNow.AddDays(-1);

            await RegistrarAtendimento(Veterinario.Id, "Hidroterapia", 4, ontem);
            await RegistrarAtendimento(Veterinario.Id, "Hidroterapia", 6, ontem);

            var resultado = await CriarRelatorio(ComoAdministrador())
                .Executar(DateTime.Today.AddDays(-7), DateTime.Today);

            resultado.Dados!.MediaEscalaDor.Should().Be(5.0m);
        }

        [Fact]
        public async Task Veterinario_ve_apenas_a_propria_produtividade()
        {
            var ontem = DateTime.UtcNow.AddDays(-1);
            var segundo = await CriarSegundoVeterinario();

            await RegistrarAtendimento(Veterinario.Id, "Hidroterapia", 4, ontem);
            await RegistrarAtendimento(segundo.Id, "Laserterapia", 6, ontem);

            var comoAdministrador = await CriarRelatorio(ComoAdministrador())
                .Executar(DateTime.Today.AddDays(-7), DateTime.Today);

            var comoVeterinario = await CriarRelatorio(ComoVeterinario())
                .Executar(DateTime.Today.AddDays(-7), DateTime.Today);

            // RN-008: o administrador vê a clínica toda; o veterinário, apenas o seu recorte.
            comoAdministrador.Dados!.TotalAtendimentos.Should().Be(2);
            comoVeterinario.Dados!.TotalAtendimentos.Should().Be(1);
            comoVeterinario.Dados.PorVeterinario.Should().ContainSingle();
        }

        [Fact]
        public async Task Atendimento_fora_do_periodo_nao_entra_no_relatorio()
        {
            await RegistrarAtendimento(Veterinario.Id, "Hidroterapia", 4, DateTime.UtcNow.AddDays(-60));

            var resultado = await CriarRelatorio(ComoAdministrador())
                .Executar(DateTime.Today.AddDays(-7), DateTime.Today);

            resultado.Dados!.TotalAtendimentos.Should().Be(0);
        }

        [Fact]
        public void Tamanho_de_pagina_acima_do_limite_e_reduzido()
        {
            var parametros = new ParametrosPagina { Tamanho = 5000 };

            parametros.Tamanho.Should().Be(100, "uma requisição não pode pedir a base inteira");
        }

        [Fact]
        public void Pagina_invalida_volta_para_a_primeira()
        {
            var parametros = new ParametrosPagina { Pagina = 0, Tamanho = 20 };

            parametros.PaginaSegura.Should().Be(1);
            parametros.Pular.Should().Be(0);
        }

        [Fact]
        public void Calculo_de_paginas_e_consistente()
        {
            var pagina = PaginaDe<string>.Criar(new[] { "a", "b" }, pagina: 2, tamanho: 2, total: 5);

            pagina.TotalDePaginas.Should().Be(3);
            pagina.TemAnterior.Should().BeTrue();
            pagina.TemProxima.Should().BeTrue();
        }

        [Fact]
        public async Task Listagem_de_pacientes_respeita_o_tamanho_da_pagina()
        {
            for (var i = 0; i < 7; i++)
            {
                Contexto.Pets.Add(new Pet
                {
                    ClinicaId = Clinica.Id,
                    TutorId = Tutor.Id,
                    Nome = $"Paciente {i:00}",
                    Especie = "Cachorro",
                    DataNascimento = DateTime.SpecifyKind(new DateTime(2021, 1, 1), DateTimeKind.Utc)
                });
            }

            await Contexto.SaveChangesAsync();

            var pagina = await new PetRepository(Contexto)
                .Listar(Clinica.Id, null, null, null, new ParametrosPagina { Pagina = 1, Tamanho = 3 });

            pagina.Itens.Should().HaveCount(3);
            pagina.Total.Should().Be(8, "sete criados aqui mais o paciente do cenário base");
            pagina.TotalDePaginas.Should().Be(3);
        }
    }
}
