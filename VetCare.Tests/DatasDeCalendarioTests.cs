using System.Text.Json;
using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// Nascimento, óbito, vacinas e validade da receita são dias de calendário: saem da API
    /// como "AAAA-MM-DD", sem a meia-noite UTC que os clientes convertiam para o dia anterior.
    /// E o filtro por dia da auditoria segue o dia da clínica, não o dia em UTC.
    /// </summary>
    public class DatasDeCalendarioTests : BaseDeTeste
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        private static DateTime Dia(int ano, int mes, int dia) =>
            DateTime.SpecifyKind(new DateTime(ano, mes, dia), DateTimeKind.Utc);

        [Fact]
        public void Datas_do_paciente_saem_sem_hora_nem_fuso()
        {
            var pet = JsonSerializer.Serialize(new PetDTO { DataNascimento = Dia(2018, 3, 14) }, Web);
            pet.Should().Contain("\"dataNascimento\":\"2018-03-14\"").And.Contain("\"dataObito\":null");

            var prontuario = JsonSerializer.Serialize(
                new ProntuarioDTO { DataNascimento = Dia(2018, 3, 14), DataObito = Dia(2026, 10, 9) }, Web);
            prontuario.Should().Contain("\"dataNascimento\":\"2018-03-14\"").And.Contain("\"dataObito\":\"2026-10-09\"");
        }

        [Fact]
        public void Datas_da_carteira_e_da_receita_saem_sem_hora_nem_fuso()
        {
            var vacina = JsonSerializer.Serialize(
                new VacinaDTO { DataAplicacao = Dia(2025, 9, 20), ProximaDose = Dia(2026, 9, 20) }, Web);
            vacina.Should().Contain("\"dataAplicacao\":\"2025-09-20\"").And.Contain("\"proximaDose\":\"2026-09-20\"");

            var receita = JsonSerializer.Serialize(new PrescricaoDTO { ValidaAte = Dia(2026, 11, 8) }, Web);
            receita.Should().Contain("\"validaAte\":\"2026-11-08\"");
        }

        [Fact]
        public void Instantes_continuam_com_hora_e_fuso()
        {
            var receita = JsonSerializer.Serialize(
                new PrescricaoDTO { DataEmissao = new DateTime(2026, 10, 9, 19, 30, 0, DateTimeKind.Utc) }, Web);

            receita.Should().Contain("\"dataEmissao\":\"2026-10-09T19:30:00Z\"");
        }

        [Fact]
        public void Data_de_calendario_e_lida_com_ou_sem_hora()
        {
            JsonSerializer.Deserialize<PetDTO>("{\"dataNascimento\":\"2018-03-14\"}", Web)!
                .DataNascimento.Should().Be(Dia(2018, 3, 14));

            var antigo = JsonSerializer.Deserialize<PetDTO>(
                "{\"dataNascimento\":\"2018-03-14T00:00:00Z\",\"dataObito\":null}", Web)!;
            antigo.DataNascimento.Should().Be(Dia(2018, 3, 14));
            antigo.DataObito.Should().BeNull();
        }

        [Fact]
        public async Task Filtro_da_auditoria_segue_o_dia_da_clinica()
        {
            // 23h30 de 09/10 em Brasília já é 10/10 em UTC; 22h de 08/10 já é 09/10 em UTC.
            Contexto.RegistrosAuditoria.AddRange(
                Registro(new DateTime(2026, 10, 10, 2, 30, 0, DateTimeKind.Utc), "23h30 de 09/10"),
                Registro(new DateTime(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc), "22h de 08/10"));
            await Contexto.SaveChangesAsync();

            var pagina = await new AuditoriaRepository(Contexto).Listar(
                Clinica.Id, null, null, null, new DateTime(2026, 10, 9), new DateTime(2026, 10, 9), new ParametrosPagina());

            pagina.Itens.Select(r => r.Detalhe).Should().Equal("23h30 de 09/10");
        }

        private RegistroAuditoria Registro(DateTime dataHora, string detalhe) => new()
        {
            ClinicaId = Clinica.Id,
            UsuarioId = UsuarioAdministrador.Id,
            NomeUsuario = UsuarioAdministrador.Nome,
            Perfil = Perfis.Administrador,
            Acao = "Consulta",
            Entidade = "Prontuario",
            Detalhe = detalhe,
            DataHora = dataHora
        };
    }
}
