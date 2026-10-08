namespace VetCare.API.DTOs
{
    /// <summary>
    /// Paciente ativo para os seletores dos formulários (agendamento, mensagens). Vem sem
    /// paginação porque a lista inteira precisa estar no seletor; por isso é enxuto.
    /// </summary>
    public class PetSelecaoDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public Guid TutorId { get; set; }

        /// <summary>Usuário de acesso do tutor, para ligar o paciente a uma conversa.</summary>
        public Guid TutorUsuarioId { get; set; }
        public string NomeTutor { get; set; } = string.Empty;
        public Guid? VeterinarioResponsavelId { get; set; }
    }
}
