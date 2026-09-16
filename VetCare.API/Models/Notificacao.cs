namespace VetCare.API.Models
{
    /// <summary>HU-015: notificações in-app dos quatro gatilhos previstos no Documento de Visão.</summary>
    public class Notificacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }

        /// <summary>SessaoAgendada, LembreteConfirmacao, NovoRegistroProntuario ou NovaMensagem.</summary>
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string? LinkRelacionado { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public bool Visualizada { get; set; }

        public Usuario? Usuario { get; set; }
    }
}
