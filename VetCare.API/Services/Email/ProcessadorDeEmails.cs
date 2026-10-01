namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Consome a <see cref="FilaDeEmails"/> e envia cada mensagem pelo canal configurado.
    /// Uma falha momentânea do servidor de e-mail é tentada de novo com espera crescente;
    /// só depois disso a mensagem é descartada, com o motivo no log.
    /// </summary>
    public class ProcessadorDeEmails : BackgroundService
    {
        private static readonly TimeSpan[] EsperasEntreTentativas =
        {
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2)
        };

        private readonly FilaDeEmails _fila;
        private readonly IServicoDeEmail _servico;
        private readonly ILogger<ProcessadorDeEmails> _logger;

        public ProcessadorDeEmails(FilaDeEmails fila, IServicoDeEmail servico, ILogger<ProcessadorDeEmails> logger)
        {
            _fila = fila;
            _servico = servico;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Envio de e-mails ativo: {Canal}.", _servico.Descricao);

            try
            {
                await foreach (var mensagem in _fila.Consumir(stoppingToken))
                {
                    await EnviarComNovasTentativas(mensagem, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Encerramento normal da aplicação.
            }
        }

        private async Task EnviarComNovasTentativas(MensagemDeEmail mensagem, CancellationToken stoppingToken)
        {
            for (var tentativa = 0; ; tentativa++)
            {
                try
                {
                    await _servico.Enviar(mensagem, stoppingToken);
                    return;
                }
                catch (Exception excecao) when (excecao is not OperationCanceledException)
                {
                    if (tentativa >= EsperasEntreTentativas.Length)
                    {
                        _logger.LogError(excecao,
                            "E-mail \"{Assunto}\" para {Destinatario} descartado após {Tentativas} tentativas.",
                            mensagem.Assunto, mensagem.Destinatario, tentativa + 1);
                        return;
                    }

                    var espera = EsperasEntreTentativas[tentativa];

                    _logger.LogWarning(excecao,
                        "Falha ao enviar e-mail \"{Assunto}\" para {Destinatario}. Nova tentativa em {Espera}.",
                        mensagem.Assunto, mensagem.Destinatario, espera);

                    await Task.Delay(espera, stoppingToken);
                }
            }
        }
    }
}
