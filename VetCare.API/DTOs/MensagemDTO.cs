namespace VetCare.API.DTOs
{
    public class MensagemDTO
    {
        public Guid Id { get; set; }
        public Guid RemetenteId { get; set; }
        public string NomeRemetente { get; set; } = string.Empty;
        public Guid DestinatarioId { get; set; }
        public Guid? PacienteId { get; set; }
        public string? NomePaciente { get; set; }
        public string Conteudo { get; set; } = string.Empty;
        public DateTime DataEnvio { get; set; }
        public bool Lida { get; set; }

        /// <summary>Indica se a mensagem foi enviada pelo usuário que fez a consulta.</summary>
        public bool Propria { get; set; }
    }
}
