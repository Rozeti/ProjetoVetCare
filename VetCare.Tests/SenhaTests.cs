using FluentAssertions;
using VetCare.API.Security;

namespace VetCare.Tests
{
    /// <summary>
    /// RNF-002: as senhas são guardadas com hash. Estes testes fixam o comportamento
    /// que impede o armazenamento reversível usado antes.
    /// </summary>
    public class SenhaTests
    {
        private readonly PasswordHasher _hasher = new();

        [Fact]
        public void Hash_gerado_nao_contem_a_senha_em_texto_claro()
        {
            const string senha = "SenhaSecreta123";

            var hash = _hasher.Gerar(senha);

            hash.Should().NotContain(senha);
            hash.Should().StartWith("pbkdf2.");
        }

        [Fact]
        public void Mesma_senha_gera_hashes_diferentes_por_causa_do_salt()
        {
            var primeiro = _hasher.Gerar("SenhaSecreta123");
            var segundo = _hasher.Gerar("SenhaSecreta123");

            primeiro.Should().NotBe(segundo);
        }

        [Fact]
        public void Senha_correta_e_aceita()
        {
            var hash = _hasher.Gerar("SenhaSecreta123");

            _hasher.Verificar("SenhaSecreta123", hash, out var precisaRehash).Should().BeTrue();
            precisaRehash.Should().BeFalse();
        }

        [Fact]
        public void Senha_incorreta_e_recusada()
        {
            var hash = _hasher.Gerar("SenhaSecreta123");

            _hasher.Verificar("SenhaErrada", hash, out _).Should().BeFalse();
        }

        [Fact]
        public void Hash_no_formato_legado_e_aceito_e_marcado_para_regravacao()
        {
            // Formato usado nas primeiras versões: Base64 puro da senha, reversível.
            var legado = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("SenhaAntiga"));

            _hasher.Verificar("SenhaAntiga", legado, out var precisaRehash).Should().BeTrue();
            precisaRehash.Should().BeTrue("a conta antiga precisa ser migrada para PBKDF2 no próximo login");
        }

        [Fact]
        public void Hash_corrompido_nao_derruba_a_verificacao()
        {
            _hasher.Verificar("qualquer", "pbkdf2.naoEhNumero.$$$.###", out _).Should().BeFalse();
            _hasher.Verificar("qualquer", string.Empty, out _).Should().BeFalse();
        }

        [Fact]
        public void Senha_provisoria_tem_tamanho_util_e_e_sempre_diferente()
        {
            var primeira = _hasher.GerarSenhaProvisoria();
            var segunda = _hasher.GerarSenhaProvisoria();

            primeira.Should().HaveLength(10);
            primeira.Should().NotBe(segunda);
        }
    }
}
