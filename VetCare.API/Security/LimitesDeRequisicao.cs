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

        /// <summary>
        /// Mural de atualizações (long polling): cada requisição fica pendurada por até 30 s,
        /// então o que se limita é quantas um mesmo usuário mantém abertas ao mesmo tempo.
        /// </summary>
        public const string TempoReal = "tempo-real";
    }
}
