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
    /// Carteira de vacinação: esquemas com mais de duas doses, recorrência mensal e anual,
    /// dose concluída pelo reforço, exclusão justificada, filtro e paginação.
    /// </summary>
    public class CarteiraDeVacinacaoTests : BaseDeTeste
    {
        private GerenciarVacinasUseCase CasoDeUso(UsuarioAtual? usuario = null)
        {
            var atual = usuario ?? ComoVeterinario();

            return new GerenciarVacinasUseCase(
                new VacinaRepository(Contexto),
                new PetRepository(Contexto),
                new VeterinarioRepository(Contexto),
                Dependencias.Auditoria(Contexto, atual),
                atual);
        }

        private static DateTime Dias(int quantidade) => DateTime.UtcNow.Date.AddDays(quantidade);

        private static ParametrosPagina Pagina(int numero = 1, int tamanho = 20) => new() { Pagina = numero, Tamanho = tamanho };

        private CriarVacinaDTO Dose(
            string nome,
            int? numero,
            int? total,
            int diasAtras,
            DateTime? proxima = null,
            string recorrencia = "Nenhuma",
            string tipo = "Vacina") => new()
        {
            PacienteId = Paciente.Id,
            Tipo = tipo,
            Nome = nome,
            NumeroDose = numero,
            TotalDoses = total,
            DataAplicacao = Dias(-diasAtras),
            ProximaDose = proxima,
            Recorrencia = recorrencia
        };

        [Fact]
        public async Task Dose_2_sem_a_dose_1_registrada_e_recusada()
        {
            var resultado = await CasoDeUso().Registrar(Dose("V10", 2, 3, 10, Dias(20)));

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Validacao);
            resultado.Mensagem.Should().Contain("dose 1");
        }

        [Fact]
        public async Task Esquema_de_tres_doses_e_registrado_em_sequencia()
        {
            var casoDeUso = CasoDeUso();

            var primeira = await casoDeUso.Registrar(Dose("V10", 1, 3, 60, Dias(-30)));
            primeira.Sucesso.Should().BeTrue(primeira.Mensagem);
            primeira.Dados!.DescricaoDose.Should().Be("Dose 1 de 3");

            var segunda = await casoDeUso.Registrar(Dose("v10", 2, 3, 30, Dias(0)));
            segunda.Sucesso.Should().BeTrue(segunda.Mensagem);

            // Repetir a dose 2 é um lançamento errado: a próxima é a 3.
            var repetida = await casoDeUso.Registrar(Dose("V10", 2, 3, 1, Dias(29)));
            repetida.Sucesso.Should().BeFalse();
            repetida.Mensagem.Should().Contain("dose 3");

            var terceira = await casoDeUso.Registrar(Dose("V10", 3, 3, 1, recorrencia: "Anual"));
            terceira.Sucesso.Should().BeTrue(terceira.Mensagem);
            terceira.Dados!.ProximaDose.Should().Be(Dias(-1).AddYears(1));

            // Esquema concluído: uma nova dose 2 é recusada, mas um esquema novo começa pela dose 1.
            var depoisDoFim = await casoDeUso.Registrar(Dose("V10", 2, 3, 0, Dias(30)));
            depoisDoFim.Sucesso.Should().BeFalse();
            depoisDoFim.Mensagem.Should().Contain("concluído");

            var lista = await casoDeUso.ListarPorPaciente(Paciente.Id, null, null, null, Pagina());
            lista.Dados!.Total.Should().Be(3);

            // As doses 1 e 2 tiveram a dose seguinte aplicada; só a última continua pendente.
            lista.Dados.Itens.Where(v => v.NumeroDose < 3).Should().OnlyContain(v => v.SituacaoDose == "Concluída");
            lista.Dados.Itens.Single(v => v.NumeroDose == 3).SituacaoDose.Should().Be("Em dia");
        }

        [Fact]
        public async Task Total_de_doses_diferente_do_esquema_iniciado_e_recusado()
        {
            var casoDeUso = CasoDeUso();

            (await casoDeUso.Registrar(Dose("Giárdia", 1, 2, 30, Dias(0)))).Sucesso.Should().BeTrue();

            var resultado = await casoDeUso.Registrar(Dose("Giárdia", 2, 3, 0, Dias(30)));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("2 doses");
        }

        [Fact]
        public async Task Numero_da_dose_maior_que_o_total_e_recusado()
        {
            var resultado = await CasoDeUso().Registrar(Dose("V8", 4, 3, 0));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("entre 1 e 3");
        }

        [Fact]
        public async Task Numero_da_dose_sem_o_total_e_recusado()
        {
            var resultado = await CasoDeUso().Registrar(Dose("V8", 1, null, 0));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("total de doses");
        }

        [Fact]
        public async Task Dose_intermediaria_sem_proxima_dose_nem_recorrencia_e_recusada()
        {
            var resultado = await CasoDeUso().Registrar(Dose("V10", 1, 3, 0));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("próxima dose");
        }

        [Theory]
        [InlineData("Mensal", 1, 0)]
        [InlineData("Trimestral", 3, 0)]
        [InlineData("Semestral", 6, 0)]
        [InlineData("Anual", 0, 1)]
        public async Task Recorrencia_calcula_a_proxima_dose_quando_a_data_nao_e_informada(string recorrencia, int meses, int anos)
        {
            var aplicacao = Dias(-3);

            var resultado = await CasoDeUso().Registrar(
                Dose("Antipulgas mensal", null, null, 3, recorrencia: recorrencia, tipo: "Antipulgas"));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Recorrencia.Should().Be(recorrencia);
            resultado.Dados.ProximaDose.Should().Be(aplicacao.AddMonths(meses).AddYears(anos));
        }

        [Fact]
        public async Task Recorrencia_mensal_serve_a_um_esquema_em_aberto()
        {
            // Dose 1 de 3 com recorrência mensal: a dose 2 fica marcada para daqui a um mês.
            var resultado = await CasoDeUso().Registrar(Dose("Vermífugo", 1, 3, 0, recorrencia: "mensal", tipo: "Vermifugo"));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.Recorrencia.Should().Be("Mensal");
            resultado.Dados.ProximaDose.Should().Be(Dias(0).AddMonths(1));
        }

        [Fact]
        public async Task Recorrencia_desconhecida_e_recusada()
        {
            var resultado = await CasoDeUso().Registrar(Dose("V10", null, null, 0, recorrencia: "Quinzenal"));

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("Recorrência inválida");
        }

        [Fact]
        public async Task Data_informada_prevalece_sobre_a_recorrencia()
        {
            var resultado = await CasoDeUso().Registrar(Dose("V10", null, null, 0, Dias(200), recorrencia: "Anual"));

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            resultado.Dados!.ProximaDose.Should().Be(Dias(200));
        }

        [Fact]
        public async Task Reforco_aplicado_conclui_a_dose_anterior_e_sai_das_pendencias()
        {
            var casoDeUso = CasoDeUso();
            var repositorio = new VacinaRepository(Contexto);

            var antiga = await casoDeUso.Registrar(Dose("Antirrábica", null, null, 370, Dias(-5)));
            antiga.Sucesso.Should().BeTrue(antiga.Mensagem);
            antiga.Dados!.SituacaoDose.Should().Be("Vencida");

            (await repositorio.ContarVencidasPorPaciente(new[] { Paciente.Id }))
                .GetValueOrDefault(Paciente.Id).Should().Be(1);

            var reforco = await casoDeUso.Registrar(Dose("antirrábica", null, null, 2, recorrencia: "Anual"));
            reforco.Sucesso.Should().BeTrue(reforco.Mensagem);

            var lista = await casoDeUso.ListarPorPaciente(Paciente.Id, null, null, null, Pagina());
            var anterior = lista.Dados!.Itens.Single(v => v.Id == antiga.Dados.Id);

            anterior.SituacaoDose.Should().Be("Concluída");
            anterior.DiasParaProximaDose.Should().BeNull();

            (await repositorio.ContarVencidasPorPaciente(new[] { Paciente.Id }))
                .GetValueOrDefault(Paciente.Id).Should().Be(0);

            var painel = await casoDeUso.ListarVencendo(30);
            painel.Dados!.Should().NotContain(v => v.Id == antiga.Dados.Id);

            // O lembrete da dose antiga também não é mais devido.
            var pendentes = await repositorio.ObterPendentesDeLembrete(Dias(7));
            pendentes.Should().NotContain(v => v.Id == antiga.Dados.Id);
        }

        [Fact]
        public async Task Filtro_por_situacao_e_tipo_respeita_a_paginacao()
        {
            var casoDeUso = CasoDeUso();

            (await casoDeUso.Registrar(Dose("V10", null, null, 400, Dias(-30)))).Sucesso.Should().BeTrue();
            (await casoDeUso.Registrar(Dose("Antirrábica", null, null, 350, Dias(10)))).Sucesso.Should().BeTrue();
            (await casoDeUso.Registrar(Dose("Vermífugo", null, null, 20, Dias(100), tipo: "Vermifugo"))).Sucesso.Should().BeTrue();
            (await casoDeUso.Registrar(Dose("Antipulgas", null, null, 5, tipo: "Antipulgas"))).Sucesso.Should().BeTrue();

            var vencidas = await casoDeUso.ListarPorPaciente(Paciente.Id, null, "Vencida", null, Pagina());
            vencidas.Dados!.Itens.Should().ContainSingle(v => v.Nome == "V10");

            var aVencer = await casoDeUso.ListarPorPaciente(Paciente.Id, null, "A vencer", null, Pagina());
            aVencer.Dados!.Itens.Should().ContainSingle(v => v.Nome == "Antirrábica");

            var emDia = await casoDeUso.ListarPorPaciente(Paciente.Id, null, "Em dia", null, Pagina());
            emDia.Dados!.Itens.Should().ContainSingle(v => v.Nome == "Vermífugo");

            var unicas = await casoDeUso.ListarPorPaciente(Paciente.Id, null, "Dose única", null, Pagina());
            unicas.Dados!.Itens.Should().ContainSingle(v => v.Nome == "Antipulgas");

            var porTipo = await casoDeUso.ListarPorPaciente(Paciente.Id, "vermifugo", null, null, Pagina());
            porTipo.Dados!.Itens.Should().ContainSingle(v => v.Tipo == "Vermifugo");

            var porTexto = await casoDeUso.ListarPorPaciente(Paciente.Id, null, null, "rráb", Pagina());
            porTexto.Dados!.Itens.Should().ContainSingle(v => v.Nome == "Antirrábica");

            var primeiraPagina = await casoDeUso.ListarPorPaciente(Paciente.Id, null, null, null, Pagina(1, 3));
            primeiraPagina.Dados!.Total.Should().Be(4);
            primeiraPagina.Dados.TotalDePaginas.Should().Be(2);
            primeiraPagina.Dados.Itens.Should().HaveCount(3);
            primeiraPagina.Dados.Itens.First().Nome.Should().Be("Antipulgas", "a aplicação mais recente vem primeiro");

            var segundaPagina = await casoDeUso.ListarPorPaciente(Paciente.Id, null, null, null, Pagina(2, 3));
            segundaPagina.Dados!.Itens.Should().ContainSingle(v => v.Nome == "V10");

            var situacaoInvalida = await casoDeUso.ListarPorPaciente(Paciente.Id, null, "Atrasada", null, Pagina());
            situacaoInvalida.Falha.Should().Be(TipoFalha.Validacao);
        }

        [Fact]
        public async Task Exclusao_exige_justificativa_e_a_guarda_na_auditoria()
        {
            var casoDeUso = CasoDeUso();

            var registro = await casoDeUso.Registrar(Dose("V10", null, null, 1));
            var id = registro.Dados!.Id;

            var semMotivo = await casoDeUso.Remover(id, "   ");
            semMotivo.Sucesso.Should().BeFalse();
            semMotivo.Falha.Should().Be(TipoFalha.Validacao);
            Contexto.Vacinas.Should().Contain(v => v.Id == id);

            var exclusao = await casoDeUso.Remover(id, "Lançada na carteira do paciente errado");
            exclusao.Sucesso.Should().BeTrue(exclusao.Mensagem);

            Contexto.Vacinas.Should().NotContain(v => v.Id == id);
            Contexto.RegistrosAuditoria.Should().Contain(r =>
                r.Entidade == "Vacina" && r.Acao == "Exclusao" && r.Detalhe.Contains("paciente errado"));
        }

        [Fact]
        public async Task Correcao_troca_o_veterinario_aplicador_dentro_da_clinica()
        {
            var usuarioColega = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Dr. Colega",
                Email = "colega@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };
            var colega = new Veterinario { UsuarioId = usuarioColega.Id, Crmv = "CRMV-DF 2000" };

            Contexto.Usuarios.Add(usuarioColega);
            Contexto.Veterinarios.Add(colega);
            await Contexto.SaveChangesAsync();

            var casoDeUso = CasoDeUso();
            var registro = await casoDeUso.Registrar(Dose("V10", 1, 3, 10, Dias(20)));
            registro.Dados!.AplicadaPor.Should().Be(UsuarioVeterinario.Nome);

            var correcao = await casoDeUso.Atualizar(registro.Dados.Id, new AtualizarVacinaDTO
            {
                VeterinarioId = colega.Id,
                Tipo = "Vacina",
                Nome = "V10",
                NumeroDose = 1,
                TotalDoses = 3,
                DataAplicacao = Dias(-10),
                ProximaDose = Dias(20)
            });

            correcao.Sucesso.Should().BeTrue(correcao.Mensagem);
            correcao.Dados!.VeterinarioId.Should().Be(colega.Id);
            correcao.Dados.AplicadaPor.Should().Be("Dr. Colega");

            var desconhecido = await casoDeUso.Atualizar(registro.Dados.Id, new AtualizarVacinaDTO
            {
                VeterinarioId = Guid.NewGuid(),
                Tipo = "Vacina",
                Nome = "V10",
                NumeroDose = 1,
                TotalDoses = 3,
                DataAplicacao = Dias(-10),
                ProximaDose = Dias(20)
            });

            desconhecido.Falha.Should().Be(TipoFalha.NaoEncontrado);
        }

        [Fact]
        public async Task Correcao_segue_as_mesmas_regras_do_registro()
        {
            var casoDeUso = CasoDeUso();
            var registro = await casoDeUso.Registrar(Dose("V10", null, null, 10));

            var futura = await casoDeUso.Atualizar(registro.Dados!.Id, new AtualizarVacinaDTO
            {
                Tipo = "Vacina",
                Nome = "V10",
                DataAplicacao = Dias(3)
            });

            futura.Sucesso.Should().BeFalse();
            futura.Mensagem.Should().Contain("futura");

            var semDose1 = await casoDeUso.Atualizar(registro.Dados.Id, new AtualizarVacinaDTO
            {
                Tipo = "Vacina",
                Nome = "V10",
                NumeroDose = 2,
                TotalDoses = 3,
                DataAplicacao = Dias(-10),
                ProximaDose = Dias(20)
            });

            semDose1.Sucesso.Should().BeFalse();
            semDose1.Mensagem.Should().Contain("dose 1");
        }

        [Fact]
        public async Task Paciente_com_obito_nao_recebe_novas_aplicacoes()
        {
            Paciente.DataObito = Dias(-1);
            Paciente.Ativo = false;
            await Contexto.SaveChangesAsync();

            var resultado = await CasoDeUso(ComoAdministrador()).Registrar(Dose("V10", null, null, 0));

            resultado.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Veterinario_so_ve_no_painel_de_prevencao_os_seus_pacientes()
        {
            var casoDeUso = CasoDeUso();
            (await casoDeUso.Registrar(Dose("Antirrábica", null, null, 350, Dias(10)))).Sucesso.Should().BeTrue();

            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Outro Vet",
                Email = "outrovet@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };
            var outroVeterinario = new Veterinario { UsuarioId = outroUsuario.Id, Crmv = "CRMV-DF 3000" };

            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Veterinarios.Add(outroVeterinario);
            await Contexto.SaveChangesAsync();

            // Cada ComoUsuario troca o contexto ambiente do HttpContextAccessor; o caso de uso é
            // montado logo antes de cada chamada para agir em nome do usuário certo.
            (await CasoDeUso(ComoVeterinario()).ListarVencendo(30)).Dados!.Should().ContainSingle();
            (await CasoDeUso(ComoUsuario(outroUsuario, Clinica.Id, veterinarioId: outroVeterinario.Id)).ListarVencendo(30))
                .Dados!.Should().BeEmpty();
            (await CasoDeUso(ComoAdministrador()).ListarVencendo(30)).Dados!.Should().ContainSingle();
        }
    }
}
