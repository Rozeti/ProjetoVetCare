namespace VetCare.API.Services.Push
{
    /// <summary>Uma notificação a ser exibida em um aparelho específico.</summary>
    public sealed record MensagemPush(
        string TokenPush,
        string Titulo,
        string Corpo,
        IReadOnlyDictionary<string, string> Dados);

    /// <summary>
    /// Resultado por aparelho. <see cref="TokenInvalido"/> indica que o aparelho não
    /// existe mais para o serviço de push (aplicativo desinstalado, permissão revogada) e
    /// que o token deve ser desativado em vez de tentado de novo.
    /// </summary>
    public sealed record ResultadoPush(string TokenPush, bool Sucesso, string? Erro, bool TokenInvalido);

    public interface IServicoDePush
    {
        bool Habilitado { get; }

        /// <summary>Envia o lote e devolve um resultado por mensagem, na mesma ordem.</summary>
        Task<IReadOnlyList<ResultadoPush>> Enviar(IReadOnlyList<MensagemPush> mensagens, CancellationToken cancelamento);
    }
}
