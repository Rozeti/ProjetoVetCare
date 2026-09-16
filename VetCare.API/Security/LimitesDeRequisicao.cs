namespace VetCare.API.Security
{
    /// <summary>
    /// Nomes das políticas de limitação de requisições. Separados em constantes para
    /// que o atributo nos controllers e o registro no Program não saiam de sincronia.
    /// </summary>
    public static class LimitesDeRequisicao
    {
        /// <summary>
        /// Login e recuperação de senha. Complementa a RN-006: enquanto o bloqueio por
        /// tentativas protege uma conta específica, este limite barra a varredura de
        /// várias contas a partir de um mesmo endereço.
        /// </summary>
        public const string Autenticacao = "autenticacao";

        /// <summary>Envio de arquivos, naturalmente mais custoso que as demais rotas.</summary>
        public const string Upload = "upload";
    }
}
