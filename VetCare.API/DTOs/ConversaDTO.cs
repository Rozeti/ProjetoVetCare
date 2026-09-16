namespace VetCare.API.DTOs
{
    /// <summary>HU-014, CA-2: item da lista de conversas com contador de não lidas.</summary>
    public class ConversaDTO
    {
        public Guid UsuarioId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
        public string UltimaMensagem { get; set; } = string.Empty;
        public DateTime DataUltimaMensagem { get; set; }
        public int NaoLidas { get; set; }

        /// <summary>Presença aproximada: considera online quem acessou nos últimos 5 minutos.</summary>
        public bool Online { get; set; }
    }
}
