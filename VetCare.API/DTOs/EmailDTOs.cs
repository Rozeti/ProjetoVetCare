namespace VetCare.API.DTOs
{
    /// <summary>Como os e-mails estão saindo do sistema, para a tela de configurações do administrador.</summary>
    public class SituacaoDeEmailDTO
    {
        /// <summary>"smtp" quando há um servidor configurado; "local" quando os e-mails ficam numa pasta da API.</summary>
        public string Canal { get; set; } = "local";

        public bool EnviaDeVerdade { get; set; }

        /// <summary>Ex.: "smtp.gmail.com:587". Nulo sem servidor configurado.</summary>
        public string? Servidor { get; set; }

        public string Remetente { get; set; } = string.Empty;
        public string NomeRemetente { get; set; } = string.Empty;

        /// <summary>Descrição completa do canal, a mesma que aparece no log de inicialização.</summary>
        public string Descricao { get; set; } = string.Empty;
    }

    public class TesteDeEmailDTO
    {
        public string Destinatario { get; set; } = string.Empty;
        public string Canal { get; set; } = "local";
        public string Mensagem { get; set; } = string.Empty;
        public int DuracaoMs { get; set; }
    }
}
