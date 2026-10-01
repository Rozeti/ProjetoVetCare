namespace VetCare.API.DTOs
{
    /// <summary>Atualização parcial: campos omitidos (nulos) mantêm o valor atual.</summary>
    public class AtualizarTratamentoDTO
    {
        public string? ObjetivoTerapeutico { get; set; }
        public string? ObservacoesGerais { get; set; }

        /// <summary>Em Andamento, Concluído ou Interrompido.</summary>
        public string? Status { get; set; }

        public DateTime? DataFim { get; set; }
    }
}
