namespace VetCare.API.Models
{
    /// <summary>
    /// Os quatro estados de uma sessão (HU-004, CA-4). Centralizados para que agenda,
    /// prontuário, indicadores e lembretes falem exatamente a mesma língua: uma grafia
    /// diferente em qualquer ponto faria uma sessão sumir dos filtros sem ninguém notar.
    /// </summary>
    public static class StatusSessao
    {
        public const string AguardandoConfirmacao = "Aguardando confirmação";
        public const string Confirmada = "Confirmada";
        public const string Cancelada = "Cancelada";
        public const string Concluida = "Concluída";

        /// <summary>Transições que podem ser pedidas pela API (HU-006 e conclusão pela equipe).</summary>
        public static readonly string[] Alteraveis = { Confirmada, Cancelada, Concluida };

        /// <summary>Devolve a grafia canônica, ou null se o valor não for um status conhecido.</summary>
        public static string? Normalizar(string? status) =>
            Alteraveis.FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));

        /// <summary>Uma sessão encerrada não volta para a agenda nem aceita novas ações do tutor.</summary>
        public static bool Encerrada(string status) => status is Cancelada or Concluida;
    }
}
