namespace VetCare.API.Models
{
    /// <summary>
    /// Estados de um tratamento. Planejado e Em Andamento são os estados ativos: aceitam
    /// sessões e evoluções. Concluído é definitivo; Inativado preserva todo o histórico e
    /// pode ser reativado.
    /// </summary>
    public static class StatusTratamento
    {
        public const string Planejado = "Planejado";
        public const string EmAndamento = "Em Andamento";
        public const string Concluido = "Concluído";
        public const string Inativado = "Inativado";

        public static readonly string[] Todos = { Planejado, EmAndamento, Concluido, Inativado };

        /// <summary>Devolve a grafia canônica, ou null se o valor não for um status conhecido.</summary>
        public static string? Normalizar(string? status) =>
            Todos.FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));

        /// <summary>Tratamento que ainda aceita sessões na agenda e registro de evoluções.</summary>
        public static bool Ativo(string status) => status is Planejado or EmAndamento;
    }
}
