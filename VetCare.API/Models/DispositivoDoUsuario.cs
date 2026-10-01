namespace VetCare.API.Models
{
    /// <summary>
    /// Celular em que o usuário entrou no aplicativo e aceitou receber notificações. O
    /// token é emitido pelo serviço de push da Expo e identifica o aparelho; um mesmo
    /// usuário pode ter vários (celular e tablet, por exemplo).
    /// </summary>
    public class DispositivoDoUsuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }

        /// <summary>Token de push no formato ExponentPushToken[...].</summary>
        public string TokenPush { get; set; } = string.Empty;

        /// <summary>"android" ou "ios".</summary>
        public string Plataforma { get; set; } = string.Empty;
        public string NomeDoAparelho { get; set; } = string.Empty;
        public DateTime RegistradoEm { get; set; } = DateTime.UtcNow;

        /// <summary>Renovado a cada abertura do aplicativo; aparelhos parados há muito tempo deixam de receber.</summary>
        public DateTime UltimoUsoEm { get; set; } = DateTime.UtcNow;

        /// <summary>Falso quando a pessoa saiu da conta no aparelho ou o serviço de push informou que o token morreu.</summary>
        public bool Ativo { get; set; } = true;

        public Usuario? Usuario { get; set; }
    }
}
