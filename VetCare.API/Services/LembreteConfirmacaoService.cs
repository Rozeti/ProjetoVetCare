using VetCare.API.Data;

namespace VetCare.API.Services
{
    /// <summary>
    /// HU-015, CA-2: dispara o lembrete de confirmação quando a sessão se aproxima e ainda
    /// não foi confirmada. Roda em segundo plano e marca cada sessão já lembrada, de modo
    /// que o tutor não receba a mesma notificação repetidas vezes.
    /// </summary>
    public class LembreteConfirmacaoService : BackgroundService
    {
        private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromMinutes(30);

        /// <summary>Sessões nas próximas 48 horas entram na janela de lembrete.</summary>
        private static readonly TimeSpan JanelaLembrete = TimeSpan.FromHours(48);

        private readonly IServiceProvider _provedor;
        private readonly ILogger<LembreteConfirmacaoService> _logger;

        public LembreteConfirmacaoService(IServiceProvider provedor, ILogger<LembreteConfirmacaoService> logger)
        {
            _provedor = provedor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Pequeno atraso inicial para não competir com a migração e o seed na subida.
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnviarLembretesPendentes(stoppingToken);
                }
                catch (Exception excecao) when (excecao is not OperationCanceledException)
                {
                    // Uma falha aqui não pode derrubar a aplicação: o ciclo seguinte tenta de novo.
                    _logger.LogError(excecao, "Falha ao processar os lembretes de confirmação de sessão.");
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

            var sessoes = escopo.ServiceProvider.GetRequiredService<ISessaoRepository>();
            var notificacoes = escopo.ServiceProvider.GetRequiredService<NotificacaoService>();

            var pendentes = await sessoes.ObterPendentesDeLembrete(DateTime.UtcNow.Add(JanelaLembrete));

            if (pendentes.Count == 0)
            {
                return;
            }

            foreach (var sessao in pendentes)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var usuarioTutor = sessao.Tratamento?.Paciente?.Tutor?.UsuarioId;

                if (usuarioTutor.HasValue)
                {
                    await notificacoes.NotificarLembreteConfirmacao(
                        usuarioTutor.Value,
                        sessao.Tratamento?.Paciente?.Nome ?? "seu pet",
                        sessao.DataHora);
                }

                sessao.LembreteEnviado = true;
            }

            await sessoes.SalvarAlteracoes();

            _logger.LogInformation("{Total} lembrete(s) de confirmação enviado(s).", pendentes.Count);
        }
    }
}
