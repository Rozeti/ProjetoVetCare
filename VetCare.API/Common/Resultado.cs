namespace VetCare.API.Common
{
    /// <summary>
    /// Motivo da falha de um caso de uso. Serve para o controller escolher o status HTTP
    /// certo sem espalhar essa decisão pela camada de negócio.
    /// </summary>
    public enum TipoFalha
    {
        Nenhuma = 0,
        Validacao,
        NaoEncontrado,
        Conflito,

        /// <summary>Credenciais não conferem: o usuário não se identificou (401).</summary>
        NaoAutenticado,

        /// <summary>O usuário está identificado, mas o perfil não permite a ação (403).</summary>
        NaoAutorizado,

        Bloqueado
    }

    public class Resultado
    {
        public bool Sucesso { get; protected init; }
        public string Mensagem { get; protected init; } = string.Empty;
        public TipoFalha Falha { get; protected init; }

        public static Resultado Ok(string mensagem = "")
            => new() { Sucesso = true, Mensagem = mensagem };

        public static Resultado Erro(TipoFalha tipo, string mensagem)
            => new() { Sucesso = false, Falha = tipo, Mensagem = mensagem };

        public static Resultado Invalido(string mensagem)
            => Erro(TipoFalha.Validacao, mensagem);

        public static Resultado NaoEncontrado(string mensagem)
            => Erro(TipoFalha.NaoEncontrado, mensagem);

        public static Resultado Conflito(string mensagem)
            => Erro(TipoFalha.Conflito, mensagem);

        public static Resultado NaoAutorizado(string mensagem)
            => Erro(TipoFalha.NaoAutorizado, mensagem);

        public static Resultado NaoAutenticado(string mensagem)
            => Erro(TipoFalha.NaoAutenticado, mensagem);
    }

    public class Resultado<T> : Resultado
    {
        public T? Dados { get; private init; }

        public static Resultado<T> Ok(T dados, string mensagem = "")
            => new() { Sucesso = true, Dados = dados, Mensagem = mensagem };

        public new static Resultado<T> Erro(TipoFalha tipo, string mensagem)
            => new() { Sucesso = false, Falha = tipo, Mensagem = mensagem };

        public new static Resultado<T> Invalido(string mensagem)
            => Erro(TipoFalha.Validacao, mensagem);

        public new static Resultado<T> NaoEncontrado(string mensagem)
            => Erro(TipoFalha.NaoEncontrado, mensagem);

        public new static Resultado<T> Conflito(string mensagem)
            => Erro(TipoFalha.Conflito, mensagem);

        public new static Resultado<T> NaoAutorizado(string mensagem)
            => Erro(TipoFalha.NaoAutorizado, mensagem);

        public new static Resultado<T> NaoAutenticado(string mensagem)
            => Erro(TipoFalha.NaoAutenticado, mensagem);

        public static Resultado<T> Bloqueado(string mensagem)
            => Erro(TipoFalha.Bloqueado, mensagem);
    }
}
