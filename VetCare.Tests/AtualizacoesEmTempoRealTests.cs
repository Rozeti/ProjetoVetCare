using FluentAssertions;
using VetCare.API.Services;

namespace VetCare.Tests
{
    /// <summary>
    /// O mural que mantém as telas em sincronia. O que precisa valer: uma clínica nunca
    /// enxerga o movimento da outra, quem espera é acordado assim que algo acontece, e
    /// quem ficou muito tempo fora é mandado recarregar em vez de receber um retrato
    /// incompleto.
    /// </summary>
    public class AtualizacoesEmTempoRealTests
    {
        private static readonly Guid ClinicaA = Guid.NewGuid();
        private static readonly Guid ClinicaB = Guid.NewGuid();

        private static CentralDeAtualizacoes Central() => new();

        [Fact]
        public void Primeira_conexao_recebe_a_versao_atual_sem_eventos()
        {
            var central = Central();
            central.Publicar(ClinicaA, "sessoes", "atualizado");

            var leitura = central.Consultar(ClinicaA, desde: -1);

            leitura.Versao.Should().Be(1);
            leitura.Eventos.Should().BeEmpty("a tela acabou de carregar os dados por conta própria");
            leitura.Reiniciar.Should().BeFalse();
        }

        [Fact]
        public void Evento_publicado_aparece_para_quem_estava_atras()
        {
            var central = Central();

            central.Publicar(ClinicaA, "sessoes", "atualizado", "Presença confirmada.");

            var leitura = central.Consultar(ClinicaA, desde: 0);

            leitura.Eventos.Should().ContainSingle();
            leitura.Eventos[0].Recurso.Should().Be("sessoes");
            leitura.Eventos[0].Descricao.Should().Be("Presença confirmada.");
        }

        /// <summary>RN-005: o isolamento entre clínicas também vale para os avisos.</summary>
        [Fact]
        public void Clinica_nao_recebe_evento_de_outra()
        {
            var central = Central();

            central.Publicar(ClinicaB, "pets", "criado", "Paciente cadastrado.");

            central.Consultar(ClinicaA, desde: 0).Eventos.Should().BeEmpty();
            central.Consultar(ClinicaB, desde: 0).Eventos.Should().ContainSingle();
        }

        [Fact]
        public void Cliente_nao_recebe_duas_vezes_o_mesmo_evento()
        {
            var central = Central();
            central.Publicar(ClinicaA, "sessoes", "atualizado");

            var primeira = central.Consultar(ClinicaA, desde: 0);
            var segunda = central.Consultar(ClinicaA, desde: primeira.Versao);

            primeira.Eventos.Should().ContainSingle();
            segunda.Eventos.Should().BeEmpty();
        }

        [Fact]
        public void Cliente_muito_atrasado_e_mandado_recarregar()
        {
            var central = Central();

            for (var i = 0; i < CentralDeAtualizacoes.LimiteDeHistorico + 10; i++)
            {
                central.Publicar(ClinicaA, "sessoes", "atualizado");
            }

            var leitura = central.Consultar(ClinicaA, desde: 1);

            leitura.Reiniciar.Should().BeTrue();
            leitura.Versao.Should().Be(CentralDeAtualizacoes.LimiteDeHistorico + 10);
        }

        [Fact]
        public async Task Espera_devolve_na_hora_quando_ja_ha_novidade()
        {
            var central = Central();
            central.Publicar(ClinicaA, "pets", "criado");

            var leitura = await central.Aguardar(
                ClinicaA, desde: 0, TimeSpan.FromSeconds(30), CancellationToken.None);

            leitura.Eventos.Should().ContainSingle();
        }

        /// <summary>
        /// O coração do recurso: a tela pendurada acorda quando a gravação acontece, e não
        /// no próximo ciclo de um temporizador.
        /// </summary>
        [Fact]
        public async Task Quem_espera_e_acordado_pela_publicacao()
        {
            var central = Central();

            var espera = central.Aguardar(
                ClinicaA, desde: 0, TimeSpan.FromSeconds(30), CancellationToken.None);

            espera.IsCompleted.Should().BeFalse("não há nada a informar ainda");

            central.Publicar(ClinicaA, "sessoes", "atualizado", "Presença confirmada.");

            var leitura = await espera.WaitAsync(TimeSpan.FromSeconds(5));

            leitura.Eventos.Should().ContainSingle();
            leitura.Eventos[0].Descricao.Should().Be("Presença confirmada.");
        }

        [Fact]
        public async Task Evento_de_outra_clinica_nao_encerra_a_espera()
        {
            var central = Central();

            var espera = central.Aguardar(
                ClinicaA, desde: 0, TimeSpan.FromSeconds(30), CancellationToken.None);

            central.Publicar(ClinicaB, "sessoes", "atualizado");

            await Task.Delay(120);
            espera.IsCompleted.Should().BeFalse();

            central.Publicar(ClinicaA, "sessoes", "atualizado");

            (await espera.WaitAsync(TimeSpan.FromSeconds(5))).Eventos.Should().ContainSingle();
        }

        [Fact]
        public async Task Espera_termina_no_prazo_quando_nada_acontece()
        {
            var central = Central();

            var leitura = await central.Aguardar(
                ClinicaA, desde: 0, TimeSpan.FromMilliseconds(150), CancellationToken.None);

            leitura.Eventos.Should().BeEmpty();
            leitura.Reiniciar.Should().BeFalse();
        }

        [Fact]
        public async Task Varias_telas_esperando_sao_acordadas_juntas()
        {
            var central = Central();

            var esperas = Enumerable.Range(0, 5)
                .Select(_ => central.Aguardar(ClinicaA, 0, TimeSpan.FromSeconds(30), CancellationToken.None))
                .ToList();

            central.Publicar(ClinicaA, "sessoes", "atualizado");

            var leituras = await Task.WhenAll(esperas).WaitAsync(TimeSpan.FromSeconds(5));

            leituras.Should().OnlyContain(l => l.Eventos.Count == 1);
        }
    }
}
