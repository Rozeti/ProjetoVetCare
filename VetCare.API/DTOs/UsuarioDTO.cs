namespace VetCare.API.DTOs
{
    public class UsuarioDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public DateTime DataCadastro { get; set; }
        public DateTime? UltimoAcesso { get; set; }

        /// <summary>Preenchido quando o usuário é Veterinário.</summary>
        public Guid? VeterinarioId { get; set; }
        public string? Crmv { get; set; }
        public string? Especialidade { get; set; }

        /// <summary>Preenchido quando o usuário é Tutor.</summary>
        public Guid? TutorId { get; set; }
        public string? Telefone { get; set; }
        public string? Endereco { get; set; }
    }
}
