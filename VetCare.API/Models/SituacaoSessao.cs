namespace VetCare.API.Models
{
    /// <summary>
    /// Leitura da sessão para as listas e a tela de gestão, a partir do status, da inativação, do
    /// registro do atendimento e do horário. Diferente do status, não é gravada: muda com o relógio
    /// (a sessão agendada passa a aguardar registro quando começa).
    /// </summary>
    public static class SituacaoSessao
    {
        /// <summary>Ainda vai acontecer.</summary>
        public const string Agendada = "Agendada";

        /// <summary>Já começou e ainda não tem o atendimento registrado.</summary>
        public const string AguardandoRegistro = "Aguardando registro";

        /// <summary>Tem o atendimento registrado ou foi concluída.</summary>
        public const string Realizada = "Realizada";

        public const string Cancelada = "Cancelada";
        public const string Inativada = "Inativada";

        public static readonly string[] Todas = { Agendada, AguardandoRegistro, Realizada, Cancelada, Inativada };

        /// <summary>Devolve a grafia canônica, ou null se o valor não for uma situação conhecida.</summary>
        public static string? Normalizar(string? situacao) =>
            Todas.FirstOrDefault(s => string.Equals(s, situacao?.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string De(Sessao sessao, bool possuiAtendimento, DateTime agoraUtc)
        {
            if (sessao.Inativa) return Inativada;
            if (sessao.Status == StatusSessao.Cancelada) return Cancelada;
            if (possuiAtendimento || sessao.Status == StatusSessao.Concluida) return Realizada;
            return sessao.DataHora > agoraUtc ? Agendada : AguardandoRegistro;
        }
    }
}
