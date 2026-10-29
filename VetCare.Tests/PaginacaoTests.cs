using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>Paginação das listagens exigida pelo RNF-004.</summary>
    public class PaginacaoTests : BaseDeTeste
    {
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
                .Listar(Clinica.Id, new FiltroDePacientes(), new ParametrosPagina { Pagina = 1, Tamanho = 3 });

            pagina.Itens.Should().HaveCount(3);
            pagina.Total.Should().Be(8, "sete criados aqui mais o paciente do cenário base");
            pagina.TotalDePaginas.Should().Be(3);
        }

        [Fact]
        public async Task Listagem_de_tutores_respeita_o_tamanho_da_pagina()
        {
            for (var i = 0; i < 7; i++)
            {
                var usuario = new Usuario
                {
                    ClinicaId = Clinica.Id,
                    Nome = $"Tutor {i:00}",
                    Email = $"tutor{i:00}@teste.com",
                    Perfil = Perfis.Tutor,
                    SenhaHash = "hash"
                };

                Contexto.Usuarios.Add(usuario);
                Contexto.Tutores.Add(new Tutor { UsuarioId = usuario.Id });
            }

            await Contexto.SaveChangesAsync();

            var pagina = await new TutorRepository(Contexto)
                .Listar(Clinica.Id, null, new ParametrosPagina { Pagina = 3, Tamanho = 3 });

            pagina.Itens.Should().HaveCount(2, "a última página traz o que sobra de oito tutores");
            pagina.Total.Should().Be(8, "sete criados aqui mais o tutor do cenário base");
            pagina.TotalDePaginas.Should().Be(3);
            pagina.TemProxima.Should().BeFalse();
        }
    }
}
