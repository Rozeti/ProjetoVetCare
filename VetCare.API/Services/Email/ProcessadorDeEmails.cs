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
                await VerificarCanal(stoppingToken);

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

        /// <summary>
        /// Uma senha de app errada ou uma porta trocada aparecem aqui, logo na subida, com a
        /// explicação em português. A API continua no ar de qualquer forma: o problema é de
        /// configuração, e os e-mails seguem sendo tentados conforme chegam.
        /// </summary>
        private async Task VerificarCanal(CancellationToken stoppingToken)
        {
            if (!_servico.EnviaDeVerdade)
            {
                return;
            }

            try
            {
                await _servico.Verificar(stoppingToken);
                _logger.LogInformation("Servidor de e-mail verificado: conexão e autenticação aceitas.");
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                _logger.LogError(excecao,
                    "O servidor de e-mail não aceitou a configuração. {Explicacao} " +
                    "Enquanto isso, nenhum e-mail (inclusive os de recuperação de senha) será entregue.",
                    FalhasDeEmail.Descrever(excecao));
            }
        }

        private async Task EnviarComNovasTentativas(MensagemDeEmail mensagem, CancellationToken stoppingToken)
        {
            for (var tentativa = 0; ; tentativa++)
            {
                try
                {
                    await _servico.Enviar(mensagem, stoppingToken);
                    _logger.LogInformation("E-mail \"{Assunto}\" enviado para {Destinatario}.", mensagem.Assunto, mensagem.Destinatario);
                    return;
                }
                catch (Exception excecao) when (excecao is not OperationCanceledException)
                {
                    if (tentativa >= EsperasEntreTentativas.Length)
                    {
                        _logger.LogError(excecao,
                            "E-mail \"{Assunto}\" para {Destinatario} descartado após {Tentativas} tentativas. {Explicacao}",
                            mensagem.Assunto, mensagem.Destinatario, tentativa + 1, FalhasDeEmail.Descrever(excecao));
                        return;
                    }

                    var espera = EsperasEntreTentativas[tentativa];

                    _logger.LogWarning(excecao,
                        "Falha ao enviar e-mail \"{Assunto}\" para {Destinatario}: {Explicacao} Nova tentativa em {Espera}.",
                        mensagem.Assunto, mensagem.Destinatario, FalhasDeEmail.Descrever(excecao), espera);

                    await Task.Delay(espera, stoppingToken);
                }
            }
        }
    }
}
