namespace VetCare.API.Models
{
    /// <summary>Estados de uma receita. RN-004: ela é cancelada, nunca apagada.</summary>
    public static class StatusPrescricao
    {
        public const string Ativa = "Ativa";
        public const string Cancelada = "Cancelada";
    }
}
