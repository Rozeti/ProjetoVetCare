namespace VetCare.API.Models
{
    /// <summary>
    /// Credencial temporária para definir uma senha sem conhecer a atual. Cada pedido gera
    /// duas formas da mesma credencial: um token longo, que vai no link do e-mail e serve ao
    /// portal web, e um código numérico curto, que a pessoa digita no aplicativo. O banco
    /// guarda apenas os hashes; os valores originais só existem no e-mail enviado.
    /// </summary>
    public class TokenRedefinicaoSenha
    {
        /// <summary>Quantos códigos errados podem ser digitados antes de o pedido ser descartado.</summary>
        public const int MaximoTentativasDeCodigo = 5;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public string CodigoHash { get; set; } = string.Empty;

        /// <summary>Um dos valores de <see cref="FinalidadesDoToken"/>.</summary>
        public string Finalidade { get; set; } = FinalidadesDoToken.Recuperacao;

        public int TentativasDeCodigo { get; set; }
        public DateTime ExpiraEm { get; set; }
        public DateTime? UtilizadoEm { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public Usuario? Usuario { get; set; }

        public bool Valido =>
            UtilizadoEm == null &&
            ExpiraEm > DateTime.UtcNow &&
            TentativasDeCodigo < MaximoTentativasDeCodigo;
    }

    public static class FinalidadesDoToken
    {
        /// <summary>"Esqueci minha senha": vale por pouco tempo.</summary>
        public const string Recuperacao = "Recuperacao";

        /// <summary>Conta recém-criada pela clínica: a pessoa define a primeira senha com calma.</summary>
        public const string PrimeiroAcesso = "PrimeiroAcesso";
    }
}
