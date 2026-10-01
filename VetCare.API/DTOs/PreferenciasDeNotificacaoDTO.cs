namespace VetCare.API.DTOs
{
    /// <summary>HU-015: por quais canais, além do próprio sistema, o usuário quer ser avisado.</summary>
    public class PreferenciasDeNotificacaoDTO
    {
        public bool NotificarPorEmail { get; set; } = true;
        public bool NotificarPorPush { get; set; } = true;
    }
}
