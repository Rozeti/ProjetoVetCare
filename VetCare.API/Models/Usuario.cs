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

        /// <summary>Última vez em que o usuário entrou ou usou o sistema; alimenta o indicador "online" das conversas.</summary>
        public DateTime? UltimoAcesso { get; set; }

        /// <summary>HU-015: além do aviso dentro do sistema, o usuário recebe cada notificação por e-mail.</summary>
        public bool NotificarPorEmail { get; set; } = true;

        /// <summary>HU-015: além do aviso dentro do sistema, o usuário recebe cada notificação no celular.</summary>
        public bool NotificarPorPush { get; set; } = true;

        public Clinica? Clinica { get; set; }
    }
}
