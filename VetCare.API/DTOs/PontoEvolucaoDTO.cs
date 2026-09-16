namespace VetCare.API.DTOs
{
    /// <summary>Ponto de uma série temporal do prontuário (peso ou escala de dor).</summary>
    public class PontoEvolucaoDTO
    {
        public DateTime Data { get; set; }
        public decimal Valor { get; set; }
    }
}
