namespace VetCare.API.Services
{
    /// <summary>
    /// Campainha entre quem grava uma notificação e o <see cref="EntregadorDeNotificacoes"/>.
    /// Sem ela o entregador só descobriria a novidade na próxima varredura periódica;
    /// com ela, o e-mail e o push saem segundos depois do aviso aparecer no sistema.
    /// </summary>
    public sealed class SinalDeNotificacoes
    {
        private readonly SemaphoreSlim _semaforo = new(0, 1);

        public void Acordar()
        {
            try
            {
                _semaforo.Release();
            }
            catch (SemaphoreFullException)
            {
                // Já havia um toque pendente: um basta para acordar o entregador.
            }
        }

        /// <summary>Espera um toque ou o prazo, o que vier primeiro.</summary>
        public Task<bool> Aguardar(TimeSpan prazo, CancellationToken cancelamento) =>
            _semaforo.WaitAsync(prazo, cancelamento);
    }
}
