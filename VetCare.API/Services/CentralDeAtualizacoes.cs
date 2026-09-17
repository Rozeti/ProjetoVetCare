namespace VetCare.API.Services
{
    /// <summary>
    /// Um fato já gravado no banco, anunciado para que as telas abertas se atualizem
    /// sozinhas. Cada evento pertence a uma clínica e recebe um número de versão
    /// crescente, que é o que permite a um cliente pedir "o que mudou depois de X".
    /// </summary>
    public sealed record EventoDeAtualizacao
    {
        public long Versao { get; init; }
        public Guid ClinicaId { get; init; }

        /// <summary>Recurso afetado em minúsculas, como "sessoes" ou "pets".</summary>
        public string Recurso { get; init; } = string.Empty;

        /// <summary>"criado", "atualizado" ou "removido".</summary>
        public string Acao { get; init; } = string.Empty;

        /// <summary>Texto pronto para leitura, vindo da própria resposta da operação.</summary>
        public string Descricao { get; init; } = string.Empty;

        public Guid AutorId { get; init; }
        public string Autor { get; init; } = string.Empty;
        public DateTime Em { get; init; } = DateTime.UtcNow;
    }

    /// <summary>Resposta de uma consulta ao mural, já recortada para uma clínica.</summary>
    public sealed record LeituraDeAtualizacoes(
        long Versao,
        IReadOnlyList<EventoDeAtualizacao> Eventos,
        bool Reiniciar);

    /// <summary>
    /// Mural de alterações do sistema. Toda operação que grava no banco publica aqui o
    /// que aconteceu, e as telas abertas — do veterinário, do apoio, do administrativo e
    /// do tutor — pedem a novidade por uma requisição que fica pendurada até haver algo a
    /// dizer (long polling). É esse desenho que faz a confirmação de presença feita no
    /// celular do tutor aparecer na agenda da clínica sem ninguém apertar "atualizar".
    ///
    /// O histórico vive em memória e é intencionalmente curto: ele não substitui o banco,
    /// apenas avisa que o banco mudou. Um cliente que ficou para trás recebe
    /// <see cref="LeituraDeAtualizacoes.Reiniciar"/> e simplesmente recarrega os dados.
    /// </summary>
    public sealed class CentralDeAtualizacoes
    {
        /// <summary>Quantos eventos ficam guardados para quem chega atrasado.</summary>
        public const int LimiteDeHistorico = 300;

        private readonly object _trava = new();
        private readonly Queue<EventoDeAtualizacao> _historico = new();

        /// <summary>Maior versão já publicada; é o "relógio" do mural.</summary>
        private long _versaoAtual;

        /// <summary>Maior versão já descartada do histórico por excesso de eventos.</summary>
        private long _versaoDescartada;

        /// <summary>
        /// Sinal compartilhado por quem está esperando. Cada publicação completa o sinal
        /// atual e cria outro, acordando todos os pendurados de uma vez.
        /// </summary>
        private TaskCompletionSource _sinal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long VersaoAtual
        {
            get
            {
                lock (_trava)
                {
                    return _versaoAtual;
                }
            }
        }

        public EventoDeAtualizacao Publicar(
            Guid clinicaId,
            string recurso,
            string acao,
            string descricao = "",
            Guid autorId = default,
            string autor = "")
        {
            EventoDeAtualizacao evento;
            TaskCompletionSource sinalAnterior;

            lock (_trava)
            {
                evento = new EventoDeAtualizacao
                {
                    Versao = ++_versaoAtual,
                    ClinicaId = clinicaId,
                    Recurso = recurso,
                    Acao = acao,
                    Descricao = descricao,
                    AutorId = autorId,
                    Autor = autor
                };

                _historico.Enqueue(evento);

                while (_historico.Count > LimiteDeHistorico)
                {
                    _versaoDescartada = _historico.Dequeue().Versao;
                }

                sinalAnterior = _sinal;
                _sinal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            // Fora da trava: as continuações de quem estava esperando não devem rodar
            // com o cadeado na mão.
            sinalAnterior.TrySetResult();

            return evento;
        }

        /// <summary>Leitura imediata, sem esperar por novidades.</summary>
        public LeituraDeAtualizacoes Consultar(Guid clinicaId, long desde)
        {
            lock (_trava)
            {
                return ConsultarSemTrava(clinicaId, desde);
            }
        }

        /// <summary>
        /// Devolve o que mudou depois de <paramref name="desde"/>. Se nada mudou, a
        /// chamada fica pendurada até surgir um evento ou até o prazo acabar — e é essa
        /// espera que troca o "atualizar de minuto em minuto" por uma reação imediata.
        ///
        /// <paramref name="desde"/> negativo significa primeira conexão: devolvemos a
        /// versão corrente na hora, para o cliente ter de onde continuar.
        /// </summary>
        public async Task<LeituraDeAtualizacoes> Aguardar(
            Guid clinicaId,
            long desde,
            TimeSpan espera,
            CancellationToken cancelamento)
        {
            var prazoFinal = DateTime.UtcNow + espera;

            while (true)
            {
                Task sinal;

                lock (_trava)
                {
                    var leitura = ConsultarSemTrava(clinicaId, desde);

                    if (desde < 0 || leitura.Reiniciar || leitura.Eventos.Count > 0)
                    {
                        return leitura;
                    }

                    sinal = _sinal.Task;
                }

                var restante = prazoFinal - DateTime.UtcNow;

                if (restante <= TimeSpan.Zero)
                {
                    return Consultar(clinicaId, desde);
                }

                try
                {
                    await sinal.WaitAsync(restante, cancelamento);
                }
                catch (TimeoutException)
                {
                    return Consultar(clinicaId, desde);
                }
                catch (OperationCanceledException)
                {
                    // O cliente desistiu da requisição: nada a entregar.
                    return Consultar(clinicaId, desde);
                }
            }
        }

        private LeituraDeAtualizacoes ConsultarSemTrava(Guid clinicaId, long desde)
        {
            if (desde < 0)
            {
                return new LeituraDeAtualizacoes(_versaoAtual, Array.Empty<EventoDeAtualizacao>(), false);
            }

            // O cliente pediu a partir de uma versão que já saiu do histórico: não dá para
            // dizer o que ele perdeu, então pedimos uma recarga completa.
            if (desde < _versaoDescartada)
            {
                return new LeituraDeAtualizacoes(_versaoAtual, Array.Empty<EventoDeAtualizacao>(), true);
            }

            var eventos = _historico
                .Where(e => e.Versao > desde && e.ClinicaId == clinicaId)
                .ToList();

            return new LeituraDeAtualizacoes(_versaoAtual, eventos, false);
        }
    }
}
