using VetCare.API.Data;

namespace VetCare.API.Services
{
    /// <summary>
    /// Avisa o tutor quando a próxima dose de uma vacina ou vermífugo se aproxima.
    /// A prevenção em dia é uma das razões de o tutor acompanhar o sistema, e depender
    /// de ele lembrar sozinho da data seria transferir para ele um controle que o
    /// sistema já tem.
    /// </summary>
    public class LembreteVacinacaoService : BackgroundService
    {
        private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromHours(6);

        /// <summary>Doses previstas para os próximos 7 dias entram na janela de aviso.</summary>
        private static readonly TimeSpan JanelaLembrete = TimeSpan.FromDays(7);

        private readonly IServiceProvider _provedor;
        private readonly ILogger<LembreteVacinacaoService> _logger;

        public LembreteVacinacaoService(IServiceProvider provedor, ILogger<LembreteVacinacaoService> logger)
        {
            _provedor = provedor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnviarLembretesPendentes(stoppingToken);
                }
                catch (Exception excecao) when (excecao is not OperationCanceledException)
                {
                    _logger.LogError(excecao, "Falha ao processar os lembretes de vacinação.");
                }

                try
                {
                    await Task.Delay(IntervaloVerificacao, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task EnviarLembretesPendentes(CancellationToken stoppingToken)
        {
            using var escopo = _provedor.CreateScope();

            var vacinas = escopo.ServiceProvider.GetRequiredService<IVacinaRepository>();
            var notificacoes = escopo.ServiceProvider.GetRequiredService<NotificacaoService>();

            var pendentes = await vacinas.ObterPendentesDeLembrete(DateTime.UtcNow.Date.Add(JanelaLembrete));

            if (pendentes.Count == 0)
            {
                return;
            }

            foreach (var vacina in pendentes)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var usuarioTutor = vacina.Paciente?.Tutor?.UsuarioId;

                if (usuarioTutor.HasValue && vacina.ProximaDose.HasValue)
                {
                    await notificacoes.NotificarDoseDeVacina(
                        usuarioTutor.Value,
                        vacina.Paciente?.Nome ?? "seu pet",
                        vacina.Nome,
                        vacina.ProximaDose.Value,
                        vacina.PacienteId);
                }

                vacina.LembreteEnviado = true;
            }

            await vacinas.SalvarAlteracoes();

            _logger.LogInformation("{Total} lembrete(s) de vacinação enviado(s).", pendentes.Count);
        }
    }
}
