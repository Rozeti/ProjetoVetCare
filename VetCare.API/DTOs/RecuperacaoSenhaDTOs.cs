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
        /// Preenchido apenas em ambiente de desenvolvimento, enquanto o envio por e-mail não
        /// está conectado. Em produção o campo nem aparece na resposta: anunciar a existência
        /// de um atalho de desenvolvimento já é informação demais para quem estiver sondando.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TokenDesenvolvimento { get; set; }
    }

    public class RedefinirComTokenDTO
    {
        [Required(ErrorMessage = "Token inválido.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a nova senha.")]
        [StringLength(64, MinimumLength = 6, ErrorMessage = "A nova senha deve ter no mínimo 6 caracteres.")]
        public string NovaSenha { get; set; } = string.Empty;
    }
}
