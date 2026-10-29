import { useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  Activity,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  ClipboardPlus,
  History,
  ListFilter,
  Pencil,
  Power,
  RotateCcw,
  Stethoscope,
} from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { ItemLinhaTempo, StatusTratamento, Tratamento } from '../../types';
import { Card, Etiqueta, SemDados } from '../../components/ui';
import {
  estiloEscalaDor,
  estiloStatusTratamento,
  formatarData,
  formatarDataHora,
  paraValorInputData,
  tratamentoAtivo,
} from '../../utils/formato';

interface Props {
  pacienteId: string;
  tratamentos: Tratamento[];
  /** Evoluções do animal (eventos da linha do tempo), separadas aqui por tratamento. */
  evolucoes: ItemLinhaTempo[];
  /** Tratamento em destaque, vindo de um atalho da avaliação ou da linha do tempo. */
  emFoco?: string;
  /** Administrador e veterinário com o paciente em acompanhamento. */
  podeGerenciar: boolean;
  /** Link para a tela de evoluções, só para quem tem acesso a ela. */
  exibeLinkDeEvolucoes: boolean;
  aoEditar: (tratamento: Tratamento) => void;
  /** Registra uma sessão realizada, que vira a evolução seguinte do tratamento. */
  aoRegistrarSessao?: (tratamento: Tratamento) => void;
  aoAbrirEvolucao: (evolucao: ItemLinhaTempo) => void;
  aoVerAvaliacao: (avaliacaoId: string) => void;
  aoVerHistorico: () => void;
  aoAtualizar: (mensagem: string) => void;
  aoFalhar: (mensagem: string) => void;
}

/**
 * Tratamentos do animal, cada um ligado à avaliação de origem. Dentro de um tratamento
 * aparecem só as evoluções dele, em ordem cronológica e com filtro; o histórico completo do
 * animal continua a um clique, na linha do tempo.
 */
export function TratamentosDoPaciente({
  pacienteId,
  tratamentos,
  evolucoes,
  emFoco,
  podeGerenciar,
  exibeLinkDeEvolucoes,
  aoEditar,
  aoRegistrarSessao,
  aoAbrirEvolucao,
  aoVerAvaliacao,
  aoVerHistorico,
  aoAtualizar,
  aoFalhar,
}: Props) {
  // O tratamento em foco abre expandido; clicar alterna a partir daí.
  const [alternados, setAlternados] = useState<Set<string>>(new Set());
  const [alterando, setAlterando] = useState('');
  const { confirmar } = useConfirmacao();

  function alternar(id: string) {
    setAlternados((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  async function mudarStatus(tratamento: Tratamento, status: StatusTratamento) {
    const textos: Record<string, { titulo: string; mensagem: string; rotulo: string; perigo?: boolean }> = {
      Concluído: {
        titulo: 'Concluir tratamento',
        mensagem: `Concluir "${tratamento.nome}"? Um tratamento concluído não pode ser reaberto: um novo acompanhamento parte de uma nova avaliação.`,
        rotulo: 'Concluir',
      },
      Inativado: {
        titulo: 'Inativar tratamento',
        mensagem: `Inativar "${tratamento.nome}"? Nada é apagado: as sessões e evoluções continuam no histórico, e o tratamento pode ser reativado depois.`,
        rotulo: 'Inativar',
        perigo: true,
      },
      'Em Andamento': {
        titulo: 'Reativar tratamento',
        mensagem: `Reativar "${tratamento.nome}"? Ele volta a aceitar sessões e evoluções.`,
        rotulo: 'Reativar',
      },
    };

    const texto = textos[status];

    if (
      texto &&
      !(await confirmar({ titulo: texto.titulo, mensagem: texto.mensagem, rotuloConfirmar: texto.rotulo, perigo: texto.perigo }))
    ) {
      return;
    }

    setAlterando(tratamento.id);

    try {
      await api.put(`/api/tratamentos/${tratamento.id}`, { status });
      aoAtualizar(
        status === 'Inativado'
          ? 'Tratamento inativado. As sessões e evoluções continuam no histórico.'
          : status === 'Concluído'
            ? 'Tratamento concluído.'
            : 'Tratamento reativado.',
      );
    } catch (falha) {
      aoFalhar(mensagemDeErro(falha, 'Não foi possível alterar a situação do tratamento.'));
    } finally {
      setAlterando('');
    }
  }

  if (tratamentos.length === 0) {
    return (
      <Card>
        <SemDados
          icone={<Stethoscope size={40} />}
          titulo="Nenhum tratamento registrado"
          descricao="Os tratamentos são criados a partir de uma avaliação: abra a aba Avaliações e use “Criar tratamento” na avaliação de origem."
        />
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {aoRegistrarSessao && tratamentos.some((t) => tratamentoAtivo(t.status)) && (
        <p className="rounded-xl bg-brand-50 px-4 py-3 text-sm text-slate-700">
          Cada sessão de fisioterapia realizada vira uma evolução numerada do tratamento: use{' '}
          <strong>Registrar sessão</strong> no tratamento ao fim de cada sessão. As sessões agendadas também podem
          ser registradas pela Agenda.
        </p>
      )}

      {tratamentos.map((tratamento) => {
        const aberto = (tratamento.id === emFoco) !== alternados.has(tratamento.id);
        const doTratamento = evolucoes
          .filter((e) => e.tratamentoId === tratamento.id)
          .sort((a, b) => a.data.localeCompare(b.data));
        const progresso = Math.min(100, (tratamento.evolucoesRegistradas / Math.max(1, tratamento.sessoesPrevistas)) * 100);

        return (
          <Card key={tratamento.id} className={`p-5 ${tratamento.id === emFoco ? 'ring-2 ring-brand-300' : ''}`}>
            <div id={`tratamento-${tratamento.id}`} className="mb-3 flex scroll-mt-24 flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="font-semibold text-slate-900">{tratamento.nome}</h3>
                  <Etiqueta className="bg-slate-100 text-slate-600">{tratamento.tipo}</Etiqueta>
                  <Etiqueta className={estiloStatusTratamento[tratamento.status] ?? 'bg-slate-200 text-slate-700'}>
                    {tratamento.status}
                  </Etiqueta>
                </div>
                <p className="mt-0.5 text-xs text-slate-500">
                  {tratamento.nomeVeterinario} · {tratamento.status === 'Planejado' ? 'Início previsto' : 'Início'} em{' '}
                  {formatarData(tratamento.dataInicio)}
                  {tratamento.dataFim && ` · Encerrado em ${formatarData(tratamento.dataFim)}`}
                </p>
                <button
                  type="button"
                  onClick={() => aoVerAvaliacao(tratamento.avaliacaoId)}
                  className="mt-0.5 text-left text-xs text-brand hover:underline"
                >
                  Avaliação de origem
                  {tratamento.dataAvaliacao && ` (${formatarData(tratamento.dataAvaliacao)})`}
                  {tratamento.motivoAvaliacao && `: ${tratamento.motivoAvaliacao}`}
                </button>
              </div>

              {podeGerenciar && (
                <div className="flex flex-wrap items-center gap-1">
                  {aoRegistrarSessao && tratamentoAtivo(tratamento.status) && (
                    <button
                      type="button"
                      onClick={() => aoRegistrarSessao(tratamento)}
                      className="vc-botao-primario mr-1 px-3 py-1.5 text-sm"
                      aria-label={`Registrar sessão de ${tratamento.nome}`}
                    >
                      <ClipboardPlus size={15} />
                      Registrar sessão
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() => aoEditar(tratamento)}
                    className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                    title="Editar tratamento"
                    aria-label={`Editar ${tratamento.nome}`}
                  >
                    <Pencil size={16} />
                  </button>

                  {tratamentoAtivo(tratamento.status) && (
                    <>
                      <button
                        type="button"
                        onClick={() => mudarStatus(tratamento, 'Concluído')}
                        disabled={alterando === tratamento.id}
                        className="rounded-lg p-2 text-slate-500 hover:bg-sucesso-claro hover:text-sucesso disabled:opacity-50"
                        title="Concluir tratamento"
                        aria-label={`Concluir ${tratamento.nome}`}
                      >
                        <CheckCircle2 size={16} />
                      </button>
                      <button
                        type="button"
                        onClick={() => mudarStatus(tratamento, 'Inativado')}
                        disabled={alterando === tratamento.id}
                        className="rounded-lg p-2 text-slate-500 hover:bg-perigo-claro hover:text-perigo disabled:opacity-50"
                        title="Inativar tratamento"
                        aria-label={`Inativar ${tratamento.nome}`}
                      >
                        <Power size={16} />
                      </button>
                    </>
                  )}

                  {tratamento.status === 'Inativado' && (
                    <button
                      type="button"
                      onClick={() => mudarStatus(tratamento, 'Em Andamento')}
                      disabled={alterando === tratamento.id}
                      className="inline-flex items-center gap-1 rounded-lg px-2 py-1.5 text-sm font-medium text-sucesso hover:bg-sucesso-claro disabled:opacity-50"
                      title="Reativar tratamento"
                    >
                      <RotateCcw size={15} />
                      Reativar
                    </button>
                  )}
                </div>
              )}
            </div>

            <p className="mb-3 whitespace-pre-line text-sm text-slate-700">{tratamento.descricao}</p>

            <dl className="mb-3 grid gap-3 text-sm sm:grid-cols-3">
              <div>
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Duração prevista</dt>
                <dd className="text-slate-700">{tratamento.duracaoPrevista}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Frequência</dt>
                <dd className="text-slate-700">{tratamento.frequencia}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Sessões previstas</dt>
                <dd className="text-slate-700">{tratamento.sessoesPrevistas}</dd>
              </div>
            </dl>

            {tratamento.observacoesGerais && (
              <p className="mb-3 text-sm text-slate-600">
                <strong className="text-slate-700">Observações:</strong> {tratamento.observacoesGerais}
              </p>
            )}

            <div className="flex items-center gap-2 text-xs text-slate-500">
              <Activity size={14} />
              {tratamento.evolucoesRegistradas} de {tratamento.sessoesPrevistas} sessões com evolução registrada
              {tratamento.totalSessoes > 0 && ` · ${tratamento.totalSessoes} na agenda`}
            </div>
            <div
              className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100"
              role="progressbar"
              aria-valuemin={0}
              aria-valuemax={tratamento.sessoesPrevistas}
              aria-valuenow={tratamento.evolucoesRegistradas}
              aria-label={`Evoluções de ${tratamento.nome}`}
            >
              <div className="h-full rounded-full bg-brand transition-all" style={{ width: `${progresso}%` }} />
            </div>

            <div className="mt-4 border-t border-slate-100 pt-3">
              <button
                type="button"
                onClick={() => alternar(tratamento.id)}
                aria-expanded={aberto}
                className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-700 hover:text-brand"
              >
                {aberto ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                Evoluções deste tratamento ({doTratamento.length})
              </button>

              {aberto && (
                <EvolucoesDoTratamento
                  evolucoes={doTratamento}
                  aoAbrir={aoAbrirEvolucao}
                  rodape={
                    <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-sm">
                      <button type="button" onClick={aoVerHistorico} className="inline-flex items-center gap-1 text-brand hover:underline">
                        <History size={14} />
                        Histórico completo do animal
                      </button>
                      {exibeLinkDeEvolucoes && (
                        <Link
                          to={`/evolucoes?pacienteId=${pacienteId}&tratamentoId=${tratamento.id}`}
                          className="inline-flex items-center gap-1 text-brand hover:underline"
                        >
                          <ListFilter size={14} />
                          Abrir na tela de evoluções
                        </Link>
                      )}
                    </div>
                  }
                />
              )}
            </div>
          </Card>
        );
      })}
    </div>
  );
}

/** Evoluções de um tratamento, em ordem cronológica, com filtro por período e número da sessão. */
function EvolucoesDoTratamento({
  evolucoes,
  aoAbrir,
  rodape,
}: {
  evolucoes: ItemLinhaTempo[];
  aoAbrir: (evolucao: ItemLinhaTempo) => void;
  rodape: ReactNode;
}) {
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [sessao, setSessao] = useState('');

  const filtradas = evolucoes.filter((evolucao) => {
    const dia = paraValorInputData(evolucao.data);
    if (de && dia < de) return false;
    if (ate && dia > ate) return false;
    if (sessao && evolucao.numeroSessao !== Number(sessao)) return false;
    return true;
  });

  const filtrando = !!de || !!ate || !!sessao;

  return (
    <div className="mt-3">
      {evolucoes.length > 0 && (
        <div className="mb-3 flex flex-wrap items-end gap-2">
          <label className="text-xs text-slate-500">
            De
            <input type="date" className="vc-campo mt-0.5 block w-auto py-1.5" value={de} onChange={(e) => setDe(e.target.value)} />
          </label>
          <label className="text-xs text-slate-500">
            Até
            <input type="date" className="vc-campo mt-0.5 block w-auto py-1.5" value={ate} onChange={(e) => setAte(e.target.value)} />
          </label>
          <label className="text-xs text-slate-500">
            Sessão nº
            <input
              type="number"
              min={1}
              className="vc-campo mt-0.5 block w-24 py-1.5"
              value={sessao}
              onChange={(e) => setSessao(e.target.value)}
            />
          </label>
          {filtrando && (
            <button
              type="button"
              className="pb-2 text-xs font-medium text-brand hover:underline"
              onClick={() => {
                setDe('');
                setAte('');
                setSessao('');
              }}
            >
              Limpar filtro
            </button>
          )}
        </div>
      )}

      {filtradas.length === 0 ? (
        <p className="rounded-xl bg-slate-50 px-4 py-4 text-center text-sm text-slate-500">
          {evolucoes.length === 0
            ? 'Nenhuma evolução registrada neste tratamento. Cada sessão realizada gera uma evolução: use “Registrar sessão” acima ou o botão da sessão na Agenda.'
            : 'Nenhuma evolução no filtro escolhido.'}
        </p>
      ) : (
        <ol className="divide-y divide-slate-100 rounded-xl border border-slate-200">
          {filtradas.map((evolucao) => (
            <li key={evolucao.chave}>
              <button
                type="button"
                onClick={() => aoAbrir(evolucao)}
                className="flex w-full items-start gap-3 px-4 py-3 text-left transition hover:bg-slate-50"
              >
                <span className="flex h-7 min-w-7 shrink-0 items-center justify-center rounded-full bg-cyan-100 px-1.5 text-xs font-bold text-cyan-800">
                  {evolucao.numeroSessao}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-slate-800">
                    {evolucao.campos['Procedimentos realizados'] ?? evolucao.descricao}
                  </span>
                  <span className="block truncate text-xs text-slate-500">{evolucao.detalhes}</span>
                  <span className="block text-xs text-slate-500">
                    {formatarDataHora(evolucao.data)}
                    {evolucao.autor && ` · ${evolucao.autor}`}
                  </span>
                </span>
                {evolucao.escalaDor != null && (
                  <Etiqueta className={estiloEscalaDor(evolucao.escalaDor)}>Dor {evolucao.escalaDor}/10</Etiqueta>
                )}
              </button>
            </li>
          ))}
        </ol>
      )}

      {rodape}
    </div>
  );
}
