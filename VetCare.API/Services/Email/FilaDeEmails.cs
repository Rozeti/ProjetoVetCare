using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Fila em memória dos e-mails transacionais (recuperação de senha, boas-vindas,
    /// avisos de segurança). Quem pede o envio não espera o servidor de e-mail responder:
    /// a requisição termina na hora e o <see cref="ProcessadorDeEmails"/> cuida do resto.
    /// As notificações do tratamento não passam por aqui — elas usam a própria tabela
    /// como fila, para sobreviver a reinícios.
    /// </summary>
    public sealed class FilaDeEmails
    {
        private readonly Channel<MensagemDeEmail> _canal =
            Channel.CreateUnbounded<MensagemDeEmail>(new UnboundedChannelOptions { SingleReader = true });

        private int _pendentes;

        public void Enfileirar(MensagemDeEmail mensagem)
        {
            if (_canal.Writer.TryWrite(mensagem))
            {
                Interlocked.Increment(ref _pendentes);
            }
        }

        /// <summary>Entrega as mensagens uma a uma, esperando quando a fila está vazia.</summary>
        public async IAsyncEnumerable<MensagemDeEmail> Consumir([EnumeratorCancellation] CancellationToken cancelamento)
        {
            await foreach (var mensagem in _canal.Reader.ReadAllAsync(cancelamento))
            {
                Interlocked.Decrement(ref _pendentes);
                yield return mensagem;
            }
        }

        /// <summary>Retira a próxima mensagem sem esperar; usado pelos testes para inspecionar a fila.</summary>
        public bool TentarRetirar(out MensagemDeEmail? mensagem)
        {
            if (_canal.Reader.TryRead(out mensagem))
            {
                Interlocked.Decrement(ref _pendentes);
                return true;
            }

            mensagem = null;
            return false;
        }

        /// <summary>Quantas mensagens ainda não foram consumidas.</summary>
        public int Pendentes => Volatile.Read(ref _pendentes);
    }
}
