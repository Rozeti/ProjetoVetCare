using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Services.Email;
using VetCare.API.Services.Push;

namespace VetCare.API.Services
{
    /// <summary>
    /// HU-015: leva cada notificação gravada no sistema até o e-mail e o celular do
    /// usuário. A tabela de notificações é a própria fila: o registro nasce "Pendente",
    /// e este serviço o marca "Entregue" ou, depois de esgotar as tentativas, "Falhou".
    /// Se a API reiniciar no meio, nada se perde — o que ficou pendente é retomado.
    ///
    /// Os canais respeitam a preferência do usuário (e-mail e push podem ser desligados
    /// em "Minha conta") e o push só vai para aparelhos em que a pessoa entrou no aplicativo.
    /// </summary>
    public class EntregadorDeNotificacoes : BackgroundService
    {
        public const int MaximoTentativas = 5;

        /// <summary>Quantas notificações são tratadas por rodada.</summary>
        private const int TamanhoDoLote = 50;

        private static readonly TimeSpan AtrasoInicial = TimeSpan.FromSeconds(15);

        /// <summary>Rede de segurança: mesmo sem sinal, o pendente é revisto neste intervalo.</summary>
        private static readonly TimeSpan IntervaloDeVarredura = TimeSpan.FromMinutes(1);

        private static readonly TimeSpan[] EsperasAposFalha =
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(1)
        };

        private readonly IServiceProvider _provedor;
        private readonly SinalDeNotificacoes _sinal;
        private readonly IServicoDeEmail _email;
        private readonly IServicoDePush _push;
        private readonly ModelosDeEmail _modelos;
        private readonly ILogger<EntregadorDeNotificacoes> _logger;

        public EntregadorDeNotificacoes(
            IServiceProvider provedor,
            SinalDeNotificacoes sinal,
            IServicoDeEmail email,
            IServicoDePush push,
            ModelosDeEmail modelos,
            ILogger<EntregadorDeNotificacoes> logger)
        {
            _provedor = provedor;
            _sinal = sinal;
            _email = email;
            _push = push;
            _modelos = modelos;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(AtrasoInicial, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EntregarPendentes(stoppingToken);
                }
                catch (Exception excecao) when (excecao is not OperationCanceledException)
                {
                    _logger.LogError(excecao, "Falha na rodada de entrega de notificações.");
                }

                try
                {
                    await _sinal.Aguardar(IntervaloDeVarredura, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Uma rodada de entrega. Público para que os testes exercitem a lógica sem
        /// esperar o laço do serviço. Devolve quantas notificações foram tratadas.
        /// </summary>
        public async Task<int> EntregarPendentes(CancellationToken cancelamento)
        {
            using var escopo = _provedor.CreateScope();

            var notificacoes = escopo.ServiceProvider.GetRequiredService<INotificacaoRepository>();
            var dispositivos = escopo.ServiceProvider.GetRequiredService<IDispositivoRepository>();

            var pendentes = await notificacoes.ObterPendentesDeEntrega(DateTime.UtcNow, TamanhoDoLote);

            if (pendentes.Count == 0)
            {
                return 0;
            }

            var aparelhosPorUsuario = _push.Habilitado
                ? (await dispositivos.ObterAtivosDosUsuarios(pendentes.Select(n => n.UsuarioId)))
                    .GroupBy(d => d.UsuarioId)
                    .ToDictionary(g => g.Key, g => g.ToList())
                : new Dictionary<Guid, List<DispositivoDoUsuario>>();

            var falhas = new Dictionary<Guid, List<string>>();
            var enviosPush = new List<(Notificacao Notificacao, DispositivoDoUsuario Aparelho, MensagemPush Mensagem)>();

            foreach (var notificacao in pendentes)
            {
                cancelamento.ThrowIfCancellationRequested();

                var usuario = notificacao.Usuario;

                if (usuario == null)
                {
                    Concluir(notificacao, SituacoesDeEntrega.Falhou, "Usuário da notificação não encontrado.");
                    continue;
                }

                // Conta desativada não recebe mais nada fora do sistema.
                if (!usuario.Ativo)
                {
                    Concluir(notificacao, SituacoesDeEntrega.Entregue, null);
                    continue;
                }

                if (usuario.NotificarPorEmail && notificacao.EmailEnviadoEm == null)
                {
                    await EnviarEmail(notificacao, usuario, falhas, cancelamento);
                }

                if (usuario.NotificarPorPush &&
                    notificacao.PushEnviadoEm == null &&
                    aparelhosPorUsuario.TryGetValue(usuario.Id, out var aparelhos))
                {
                    foreach (var aparelho in aparelhos)
                    {
                        enviosPush.Add((notificacao, aparelho, new MensagemPush(
                            aparelho.TokenPush,
                            notificacao.Titulo,
                            notificacao.Conteudo,
                            DadosDoPush(notificacao))));
                    }
                }
            }

            if (enviosPush.Count > 0)
            {
                await EnviarPush(enviosPush, dispositivos, falhas, cancelamento);
            }

            foreach (var notificacao in pendentes.Where(n => n.SituacaoEntrega == SituacoesDeEntrega.Pendente))
            {
                if (falhas.TryGetValue(notificacao.Id, out var erros))
                {
                    Reagendar(notificacao, string.Join(" | ", erros));
                }
                else
                {
                    Concluir(notificacao, SituacoesDeEntrega.Entregue, null);
                }
            }

            await notificacoes.SalvarAlteracoes();
            await dispositivos.SalvarAlteracoes();

            _logger.LogInformation(
                "Entrega de notificações: {Total} tratada(s), {Falhas} com nova tentativa agendada.",
                pendentes.Count, falhas.Count);

            return pendentes.Count;
        }

        private async Task EnviarEmail(
            Notificacao notificacao,
            Usuario usuario,
            Dictionary<Guid, List<string>> falhas,
            CancellationToken cancelamento)
        {
            try
            {
                await _email.Enviar(_modelos.Notificacao(usuario, notificacao), cancelamento);
                notificacao.EmailEnviadoEm = DateTime.UtcNow;
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                _logger.LogWarning(excecao,
                    "Falha ao enviar por e-mail a notificação {Id} para {Email}.", notificacao.Id, usuario.Email);

                AnotarFalha(falhas, notificacao.Id, $"E-mail: {excecao.Message}");
            }
        }

        private async Task EnviarPush(
            List<(Notificacao Notificacao, DispositivoDoUsuario Aparelho, MensagemPush Mensagem)> envios,
            IDispositivoRepository dispositivos,
            Dictionary<Guid, List<string>> falhas,
            CancellationToken cancelamento)
        {
            var resultados = await _push.Enviar(envios.Select(e => e.Mensagem).ToList(), cancelamento);

            // Por notificação: basta um aparelho ter recebido para considerá-la entregue;
            // uma falha passageira em todos os aparelhos vira nova tentativa.
            var balanco = new Dictionary<Guid, (bool Sucesso, bool FalhaPassageira, string? Erro)>();

            for (var i = 0; i < envios.Count && i < resultados.Count; i++)
            {
                var (notificacao, aparelho, _) = envios[i];
                var resultado = resultados[i];

                if (resultado.TokenInvalido)
                {
                    // O aparelho não existe mais para o serviço de push: parar de insistir.
                    aparelho.Ativo = false;
                    dispositivos.Atualizar(aparelho);
                }

                balanco.TryGetValue(notificacao.Id, out var atual);

                balanco[notificacao.Id] = (
                    atual.Sucesso || resultado.Sucesso,
                    atual.FalhaPassageira || (!resultado.Sucesso && !resultado.TokenInvalido),
                    resultado.Erro ?? atual.Erro);
            }

            foreach (var (notificacao, _, _) in envios.DistinctBy(e => e.Notificacao.Id))
            {
                var (sucesso, falhaPassageira, erro) = balanco[notificacao.Id];

                if (sucesso || !falhaPassageira)
                {
                    notificacao.PushEnviadoEm = DateTime.UtcNow;
                }
                else
                {
                    AnotarFalha(falhas, notificacao.Id, $"Push: {erro}");
                }
            }
        }

        private static void Concluir(Notificacao notificacao, string situacao, string? erro)
        {
            notificacao.SituacaoEntrega = situacao;
            notificacao.ProximaTentativaEm = null;
            notificacao.ErroDeEntrega = Limitar(erro);
        }

        private static void Reagendar(Notificacao notificacao, string erro)
        {
            notificacao.TentativasDeEntrega++;
            notificacao.ErroDeEntrega = Limitar(erro);

            if (notificacao.TentativasDeEntrega >= MaximoTentativas)
            {
                notificacao.SituacaoEntrega = SituacoesDeEntrega.Falhou;
                notificacao.ProximaTentativaEm = null;
                return;
            }

            var indice = Math.Min(notificacao.TentativasDeEntrega - 1, EsperasAposFalha.Length - 1);
            notificacao.ProximaTentativaEm = DateTime.UtcNow.Add(EsperasAposFalha[indice]);
        }

        private static void AnotarFalha(Dictionary<Guid, List<string>> falhas, Guid notificacaoId, string erro)
        {
            if (!falhas.TryGetValue(notificacaoId, out var lista))
            {
                falhas[notificacaoId] = lista = new List<string>();
            }

            lista.Add(erro);
        }

        /// <summary>O aplicativo usa estes dados para abrir a tela certa ao tocar na notificação.</summary>
        private static IReadOnlyDictionary<string, string> DadosDoPush(Notificacao notificacao) =>
            new Dictionary<string, string>
            {
                ["notificacaoId"] = notificacao.Id.ToString(),
                ["tipo"] = notificacao.Tipo,
                ["link"] = notificacao.LinkRelacionado ?? string.Empty
            };

        private static string? Limitar(string? valor) =>
            valor == null || valor.Length <= 500 ? valor : valor[..500];
    }
}
