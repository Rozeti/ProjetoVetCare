namespace VetCare.API.DTOs
{
    /// <summary>RN-004: a receita é cancelada, nunca apagada; o motivo fica no histórico.</summary>
    public class CancelarPrescricaoDTO
    {
        public string? Motivo { get; set; }
    }
}
