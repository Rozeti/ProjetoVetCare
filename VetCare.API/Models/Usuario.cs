namespace VetCare.API.Models
{
    public class Usuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ClinicaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;

        /// <summary>Administrador, Veterinario, Tutor ou Apoio.</summary>
        public string Perfil { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

        /// <summary>RN-006: contador de tentativas de login malsucedidas consecutivas.</summary>
        public int TentativasFalhas { get; set; }

        /// <summary>RN-006: instante até o qual novas tentativas ficam bloqueadas.</summary>
        public DateTime? BloqueadoAte { get; set; }

        public DateTime? UltimoAcesso { get; set; }

        public Clinica? Clinica { get; set; }
    }
}
