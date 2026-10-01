using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VetCare.API.DTOs
{
    public class SolicitarRecuperacaoDTO
    {
        [Required(ErrorMessage = "Informe o e-mail da conta.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// A resposta é sempre a mesma, exista a conta ou não, para não permitir que
    /// terceiros descubram quais e-mails estão cadastrados.
    /// </summary>
    public class RespostaRecuperacaoDTO
    {
        public string Mensagem { get; set; } = string.Empty;

        /// <summary>
        /// Preenchidos apenas em desenvolvimento e sem servidor de e-mail configurado, para
        /// que o fluxo possa ser percorrido sem abrir a caixa de saída local. Em produção os
        /// campos nem aparecem na resposta: anunciar a existência de um atalho de
        /// desenvolvimento já é informação demais para quem estiver sondando.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TokenDesenvolvimento { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CodigoDesenvolvimento { get; set; }
    }

    /// <summary>
    /// Define a nova senha com o token do link (portal) ou com o e-mail da conta e o
    /// código de seis dígitos (aplicativo). Um dos dois caminhos precisa vir preenchido.
    /// </summary>
    public class RedefinirSenhaDTO
    {
        public string? Token { get; set; }

        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string? Email { get; set; }

        public string? Codigo { get; set; }

        [Required(ErrorMessage = "Informe a nova senha.")]
        [StringLength(64, MinimumLength = 6, ErrorMessage = "A nova senha deve ter no mínimo 6 caracteres.")]
        public string NovaSenha { get; set; } = string.Empty;
    }
}
