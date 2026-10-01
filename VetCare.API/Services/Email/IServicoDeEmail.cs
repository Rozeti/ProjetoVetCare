namespace VetCare.API.Services.Email
{
    /// <summary>Um e-mail pronto para sair, com as duas versões do corpo que os clientes de e-mail esperam.</summary>
    public sealed record MensagemDeEmail(
        string Destinatario,
        string NomeDestinatario,
        string Assunto,
        string CorpoHtml,
        string CorpoTexto);

    /// <summary>
    /// Canal de saída de e-mails. A implementação real fala SMTP; a de desenvolvimento
    /// grava o e-mail em disco. Quem envia não precisa saber qual das duas está ativa.
    /// </summary>
    public interface IServicoDeEmail
    {
        /// <summary>Descrição curta do canal, para o log de inicialização ("SMTP smtp.gmail.com:587").</summary>
        string Descricao { get; }

        Task Enviar(MensagemDeEmail mensagem, CancellationToken cancelamento);
    }
}
