namespace VetCare.API.DTOs
{
    public class AtualizarTratamentoDTO
    {
        public string ObjetivoTerapeutico { get; set; } = string.Empty;
        public string ObservacoesGerais { get; set; } = string.Empty;

        /// <summary>Em Andamento, Concluído ou Interrompido.</summary>
        public string Status { get; set; } = string.Empty;

        public DateTime? DataFim { get; set; }
    }
}
