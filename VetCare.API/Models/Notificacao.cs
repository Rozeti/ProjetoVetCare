namespace VetCare.API.Models
{
    /// <summary>
    /// HU-015: aviso gerado por um evento do tratamento. Nasce como notificação dentro do
    /// sistema e, em seguida, é entregue por e-mail e no celular do usuário pelo
    /// <c>EntregadorDeNotificacoes</c>. O próprio registro guarda o andamento dessa
    /// entrega, o que torna a tabela a fila de saída: nada se perde se a API reiniciar.
    /// </summary>
    public class Notificacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }

        /// <summary>Um dos valores de <see cref="TiposDeNotificacao"/>.</summary>
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;

        /// <summary>Caminho dentro do portal para onde a notificação leva, como "/minha-agenda".</summary>
        public string? LinkRelacionado { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public bool Visualizada { get; set; }

        /// <summary>Um dos valores de <see cref="SituacoesDeEntrega"/>.</summary>
        public string SituacaoEntrega { get; set; } = SituacoesDeEntrega.Pendente;
        public DateTime? EmailEnviadoEm { get; set; }
        public DateTime? PushEnviadoEm { get; set; }
        public int TentativasDeEntrega { get; set; }

        /// <summary>Quando a próxima tentativa pode acontecer, após uma falha; nulo quando pode ser agora.</summary>
        public DateTime? ProximaTentativaEm { get; set; }
        public string? ErroDeEntrega { get; set; }

        public Usuario? Usuario { get; set; }
    }

    /// <summary>Gatilhos de notificação previstos na HU-015 e nos lembretes automáticos.</summary>
    public static class TiposDeNotificacao
    {
        public const string SessaoAgendada = "SessaoAgendada";
        public const string LembreteConfirmacao = "LembreteConfirmacao";
        public const string StatusSessao = "StatusSessao";
        public const string NovoRegistroProntuario = "NovoRegistroProntuario";
        public const string NovaMensagem = "NovaMensagem";
        public const string DoseDeVacina = "DoseDeVacina";
    }

    /// <summary>Andamento da entrega de uma notificação por e-mail e push.</summary>
    public static class SituacoesDeEntrega
    {
        /// <summary>Ainda não entregue em todos os canais aplicáveis; o entregador vai tentar.</summary>
        public const string Pendente = "Pendente";

        /// <summary>Todos os canais aplicáveis ao usuário foram atendidos.</summary>
        public const string Entregue = "Entregue";

        /// <summary>As tentativas se esgotaram; o motivo fica em <see cref="Notificacao.ErroDeEntrega"/>.</summary>
        public const string Falhou = "Falhou";
    }
}
