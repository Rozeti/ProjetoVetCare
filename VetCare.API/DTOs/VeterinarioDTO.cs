namespace VetCare.API.DTOs
{
    public class VeterinarioDTO
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Crmv { get; set; } = string.Empty;
        public string Especialidade { get; set; } = string.Empty;
        public bool Ativo { get; set; }

        /// <summary>HU-005, CA-1: cor usada para diferenciar o profissional na agenda geral.</summary>
        public string Cor { get; set; } = string.Empty;
    }
}
