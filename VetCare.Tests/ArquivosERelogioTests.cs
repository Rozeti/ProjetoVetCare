using System.Web;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Security;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// Duas peças de infraestrutura com regra própria: a URL assinada que protege mídias e
    /// documentos (RN-003, RNF-002) e o relógio que interpreta horários no fuso da clínica.
    /// </summary>
    public class ArquivosERelogioTests
    {
        private const string Caminho = "/uploads/3f2504e0-4f89-11d3-9a0c-0305e82c3301.jpg";

        [Fact]
        public void Url_assinada_abre_dentro_do_prazo()
        {
            var assinador = Dependencias.Assinador();

            var url = assinador.Assinar(Caminho);
            url.Should().StartWith("/api/arquivos/uploads/3f2504e0-4f89-11d3-9a0c-0305e82c3301.jpg?exp=");

            var (pasta, nome, exp, sig) = Decompor(url);

            assinador.Validar(pasta, nome, exp, sig).Should().BeTrue();
        }

        [Fact]
        public void Assinatura_alterada_ou_vencida_e_recusada()
        {
            var assinador = Dependencias.Assinador();
            var (pasta, nome, exp, sig) = Decompor(assinador.Assinar(Caminho));

            assinador.Validar(pasta, nome, exp, sig + "a").Should().BeFalse();
            assinador.Validar(pasta, nome, exp + 60, sig).Should().BeFalse("o prazo faz parte do que foi assinado");
            assinador.Validar(pasta, nome, DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(), sig).Should().BeFalse();
            assinador.Validar("documentos", nome, exp, sig).Should().BeFalse("a pasta faz parte do que foi assinado");
        }

        [Fact]
        public void Outra_instalacao_nao_consegue_assinar_para_esta()
        {
            var daqui = Dependencias.Assinador();
            var deOutra = new AssinadorDeArquivos(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Chave"] = "outra-chave-bem-diferente-com-32-bytes-ou-mais" })
                .Build());

            var (pasta, nome, exp, sig) = Decompor(deOutra.Assinar(Caminho));

            daqui.Validar(pasta, nome, exp, sig).Should().BeFalse();
        }

        [Fact]
        public void Nome_fora_do_padrao_nao_e_assinado_nem_servido()
        {
            var assinador = Dependencias.Assinador();

            assinador.Assinar("/uploads/../appsettings.json").Should().Be("/uploads/../appsettings.json");
            assinador.Validar("uploads", "../appsettings.json", long.MaxValue, "x").Should().BeFalse();
            assinador.Assinar("https://cdn.exemplo.com/foto.jpg").Should().Be("https://cdn.exemplo.com/foto.jpg");
        }

        [Fact]
        public void Horario_sem_fuso_e_lido_como_horario_da_clinica()
        {
            var relogio = new RelogioDaClinica("America/Sao_Paulo");
            var digitado = new DateTime(2026, 6, 10, 14, 0, 0, DateTimeKind.Unspecified);

            var utc = relogio.NormalizarParaUtc(digitado);

            utc.Kind.Should().Be(DateTimeKind.Utc);
            utc.Should().Be(new DateTime(2026, 6, 10, 17, 0, 0, DateTimeKind.Utc), "São Paulo fica em UTC-3 em junho");
            relogio.ParaLocal(utc).Should().Be(digitado);
        }

        [Fact]
        public void Dia_da_clinica_de_um_instante_utc_perto_da_meia_noite()
        {
            var relogio = new RelogioDaClinica("America/Sao_Paulo");

            // 02:00 em UTC ainda é o dia anterior, 23:00, em São Paulo.
            relogio.DiaDaClinica(new DateTime(2026, 6, 10, 2, 0, 0, DateTimeKind.Utc))
                .Should().Be(new DateTime(2026, 6, 9));

            var (inicio, fim) = relogio.IntervaloDoDia(new DateTime(2026, 6, 9));
            inicio.Should().Be(new DateTime(2026, 6, 9, 3, 0, 0, DateTimeKind.Utc));
            fim.Should().Be(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public void Fuso_desconhecido_cai_no_padrao_em_vez_de_derrubar_a_aplicacao()
        {
            var relogio = new RelogioDaClinica("Fuso/Inexistente");

            relogio.Fuso.Should().NotBeNull();
        }

        private static (string Pasta, string Nome, long Exp, string Sig) Decompor(string url)
        {
            var uri = new Uri("http://localhost" + url);
            var partes = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var consulta = HttpUtility.ParseQueryString(uri.Query);

            return (partes[2], partes[3], long.Parse(consulta["exp"]!), consulta["sig"]!);
        }
    }
}
