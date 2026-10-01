namespace VetCare.API.Models
{
    /// <summary>Estados de um tratamento fisioterapêutico (HU-013).</summary>
    public static class StatusTratamento
    {
        public const string EmAndamento = "Em Andamento";
        public const string Concluido = "Concluído";
        public const string Interrompido = "Interrompido";

        public static readonly string[] Todos = { EmAndamento, Concluido, Interrompido };

        /// <summary>Devolve a grafia canônica, ou null se o valor não for um status conhecido.</summary>
        public static string? Normalizar(string? status) =>
            Todos.FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
    }
}
