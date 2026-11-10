import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarPlus,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  ListFilter,
  Pencil,
  Power,
  RotateCcw,
  Stethoscope,
} from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Sessao, StatusTratamento, Tratamento } from '../../types';
import { Card, Etiqueta, SemDados } from '../../components/ui';
import { estiloStatusTratamento, formatarData, formatarDataHora, tratamentoAtivo } from '../../utils/formato';
import { ListaDeSessoes, type AcoesDaSessao } from './ListaDeSessoes';

interface Props {
  pacienteId: string;
  tratamentos: Tratamento[];
  /** Sessões cadastradas do animal; cada tratamento mostra as suas. */
  sessoes: Sessao[];
  /** Tratamento em destaque, vindo de um atalho da avaliação ou da linha do tempo: abre expandido. */
  emFoco?: string;
  /** Administrador e veterinário com o paciente em acompanhamento: editar, concluir e inativar. */
  podeGerenciar: boolean;
  /** Atalho para a tela de sessões, só para quem tem acesso a ela. */
  exibeLinkDaTelaDeSessoes: boolean;
  aoEditar: (tratamento: Tratamento) => void;
  /** Cadastro do plano de sessões, e de sessões a mais depois; ausente para quem não cadastra. */
  aoCadastrarSessoes?: (tratamento: Tratamento) => void;
  acoesDaSessao: AcoesDaSessao;
  aoVerAvaliacao: (avaliacaoId: string) => void;
  aoAtualizar: (mensagem: string) => void;
  aoFalhar: (mensagem: string) => void;
}

/** A próxima sessão do plano que ainda vai acontecer. */
function proximaSessao(sessoes: Sessao[]): Sessao | undefined {
  return sessoes
    .filter((s) => s.situacao === 'Agendada')
    .sort((a, b) => a.dataHora.localeCompare(b.dataHora))[0];
}

/**
 * Tratamentos do animal em linhas compactas: nome, situação, andamento e a próxima sessão. Ao
 * expandir, aparecem o plano completo, a avaliação de origem, as ações e as sessões do
 * tratamento, onde cada sessão realizada recebe o registro do atendimento.
 */
export function TratamentosDoPaciente({
  pacienteId,
  tratamentos,
  sessoes,
  emFoco,
  podeGerenciar,
  exibeLinkDaTelaDeSessoes,
  aoEditar,
  aoCadastrarSessoes,
  acoesDaSessao,
  aoVerAvaliacao,
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
        mensagem: `Inativar "${tratamento.nome}"? Nada é apagado: as sessões registradas continuam no histórico, e o tratamento pode ser reativado depois.`,
        rotulo: 'Inativar',
        perigo: true,
      },
      'Em Andamento': {
        titulo: 'Reativar tratamento',
        mensagem: `Reativar "${tratamento.nome}"? Ele volta a aceitar novas sessões.`,
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
          ? 'Tratamento inativado. As sessões registradas continuam no histórico.'
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
    <Card className="overflow-hidden">
      <ul className="divide-y divide-slate-100">
        {tratamentos.map((tratamento) => {
          const aberto = (tratamento.id === emFoco) !== alternados.has(tratamento.id);
          const doTratamento = sessoes.filter((s) => s.tratamentoId === tratamento.id);
          const realizadas = doTratamento.filter((s) => s.situacao === 'Realizada').length;
          const aguardandoRegistro = doTratamento.filter((s) => s.situacao === 'Aguardando registro').length;
          const agendadas = doTratamento.filter((s) => s.situacao === 'Agendada').length;
          // O plano: as canceladas e as inativadas não contam.
          const cadastradas = doTratamento.filter((s) => !s.inativa && s.situacao !== 'Cancelada').length;
          const faltamCadastrar = Math.max(0, tratamento.sessoesPrevistas - cadastradas);
          const proxima = proximaSessao(doTratamento);
          const progresso = Math.min(100, (realizadas / Math.max(1, tratamento.sessoesPrevistas)) * 100);
          const ativo = tratamentoAtivo(tratamento.status);
          const podeCadastrar = !!aoCadastrarSessoes && ativo;
          // Todas as sessões cadastradas já aconteceram e foram registradas: hora de decidir se o
          // tratamento continua (mais sessões) ou termina.
          const planoCumprido = ativo && cadastradas > 0 && agendadas === 0 && aguardandoRegistro === 0;

          return (
            <li
              key={tratamento.id}
              id={`tratamento-${tratamento.id}`}
              className={`scroll-mt-24 ${tratamento.id === emFoco ? 'bg-brand-50/40' : ''}`}
            >
              {/* Resumo: o que identifica o tratamento e como ele anda, numa linha. */}
              <button
                type="button"
                onClick={() => alternar(tratamento.id)}
                aria-expanded={aberto}
                className="flex w-full items-center gap-3 px-4 py-3 text-left transition hover:bg-slate-50 sm:px-5"
              >
                {aberto ? (
                  <ChevronDown size={18} className="shrink-0 text-slate-400" aria-hidden="true" />
                ) : (
                  <ChevronRight size={18} className="shrink-0 text-slate-400" aria-hidden="true" />
                )}

                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="truncate font-semibold text-slate-900" title={tratamento.nome}>
                      {tratamento.nome}
                    </span>
                    <Etiqueta className={estiloStatusTratamento[tratamento.status] ?? 'bg-slate-200 text-slate-700'}>
                      {tratamento.status}
                    </Etiqueta>
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-slate-500">
                    {[
                      tratamento.tipo,
                      tratamento.nomeVeterinario,
                      `${tratamento.status === 'Planejado' ? 'início previsto' : 'início'} em ${formatarData(tratamento.dataInicio)}`,
                      proxima && `próxima sessão ${formatarDataHora(proxima.dataHora)}`,
                      aguardandoRegistro > 0 &&
                        `${aguardandoRegistro} ${aguardandoRegistro === 1 ? 'sessão aguarda' : 'sessões aguardam'} registro`,
                      ativo && faltamCadastrar > 0 && `${faltamCadastrar} ${faltamCadastrar === 1 ? 'sessão' : 'sessões'} a cadastrar`,
                    ]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                </span>

                <span className="hidden shrink-0 items-center gap-2 sm:flex" title="Sessões realizadas do total previsto">
                  <span className="text-xs tabular-nums text-slate-600">
                    {realizadas}/{tratamento.sessoesPrevistas}
                  </span>
                  <span className="h-1.5 w-20 overflow-hidden rounded-full bg-slate-100" aria-hidden="true">
                    <span className="block h-full rounded-full bg-brand" style={{ width: `${progresso}%` }} />
                  </span>
                </span>
              </button>

              {aberto && (
                <div className="space-y-4 border-t border-slate-100 bg-white px-4 pb-5 pt-4 sm:px-5 sm:pl-12">
                  {podeGerenciar && (
                    <div className="flex flex-wrap items-center gap-1">
                      {podeGerenciar && (
                        <>
                          <button
                            type="button"
                            onClick={() => aoEditar(tratamento)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                            title="Editar tratamento"
                            aria-label={`Editar ${tratamento.nome}`}
                          >
                            <Pencil size={16} />
                          </button>

                          {ativo && (
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
                        </>
                      )}
                    </div>
                  )}

                  <p className="whitespace-pre-line text-sm text-slate-700">{tratamento.descricao}</p>

                  <dl className="grid gap-3 text-sm sm:grid-cols-3">
                    <div>
                      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Duração prevista</dt>
                      <dd className="text-slate-700">{tratamento.duracaoPrevista || 'Não informada'}</dd>
                    </div>
                    <div>
                      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Frequência</dt>
                      <dd className="text-slate-700">{tratamento.frequencia || 'Não informada'}</dd>
                    </div>
                    <div>
                      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Sessões</dt>
                      <dd className="text-slate-700">
                        {tratamento.sessoesPrevistas} previstas · {cadastradas} cadastradas · {realizadas}{' '}
                        {realizadas === 1 ? 'realizada' : 'realizadas'}
                      </dd>
                    </div>
                  </dl>

                  {tratamento.observacoesGerais && (
                    <p className="text-sm text-slate-600">
                      <strong className="text-slate-700">Observações:</strong> {tratamento.observacoesGerais}
                    </p>
                  )}

                  <p className="text-xs text-slate-500">
                    {tratamento.dataFim && `Encerrado em ${formatarData(tratamento.dataFim)} · `}
                    <button type="button" onClick={() => aoVerAvaliacao(tratamento.avaliacaoId)} className="text-brand hover:underline">
                      Avaliação de origem
                      {tratamento.dataAvaliacao && ` (${formatarData(tratamento.dataAvaliacao)})`}
                      {tratamento.motivoAvaliacao && `: ${tratamento.motivoAvaliacao}`}
                    </button>
                  </p>

                  <div>
                    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                      <div>
                        <h4 className="text-sm font-semibold text-slate-700">Sessões do tratamento</h4>
                        <p className="text-xs text-slate-500">
                          {cadastradas} de {tratamento.sessoesPrevistas} cadastradas · {realizadas}{' '}
                          {realizadas === 1 ? 'realizada' : 'realizadas'}
                          {exibeLinkDaTelaDeSessoes && doTratamento.length > 0 && (
                            <>
                              {' · '}
                              <Link
                                to={`/sessoes?pacienteId=${pacienteId}&tratamentoId=${tratamento.id}`}
                                className="inline-flex items-center gap-1 font-medium text-brand hover:underline"
                              >
                                <ListFilter size={12} />
                                Abrir na tela de sessões
                              </Link>
                            </>
                          )}
                        </p>
                      </div>

                      {/* Um botão só: o plano inicial e, depois, sessões a mais — no meio ou no fim do tratamento. */}
                      {podeCadastrar && !planoCumprido && (
                        <button
                          type="button"
                          onClick={() => aoCadastrarSessoes!(tratamento)}
                          className={cadastradas === 0 ? 'vc-botao-primario px-3 py-1.5 text-sm' : 'vc-botao-secundario px-3 py-1.5 text-sm'}
                        >
                          <CalendarPlus size={15} />
                          {cadastradas === 0 ? 'Cadastrar sessões' : 'Adicionar sessões'}
                        </button>
                      )}
                    </div>

                    {planoCumprido && (podeCadastrar || podeGerenciar) && (
                      <div className="mb-3 flex flex-wrap items-center gap-3 rounded-xl border border-sucesso/30 bg-sucesso-claro/60 px-4 py-3">
                        <CheckCircle2 size={18} className="shrink-0 text-sucesso" aria-hidden="true" />
                        <p className="min-w-0 flex-1 text-sm text-slate-700">
                          {cadastradas === 1
                            ? 'A sessão cadastrada foi realizada.'
                            : `As ${cadastradas} sessões cadastradas foram realizadas.`}{' '}
                          Se o animal precisar continuar, adicione mais sessões; se não, conclua o tratamento.
                        </p>
                        <span className="flex flex-wrap gap-2">
                          {podeCadastrar && (
                            <button
                              type="button"
                              onClick={() => aoCadastrarSessoes!(tratamento)}
                              className="vc-botao-primario px-3 py-1.5 text-sm"
                            >
                              <CalendarPlus size={15} />
                              Adicionar sessões
                            </button>
                          )}
                          {podeGerenciar && (
                            <button
                              type="button"
                              onClick={() => mudarStatus(tratamento, 'Concluído')}
                              disabled={alterando === tratamento.id}
                              className="vc-botao-secundario px-3 py-1.5 text-sm"
                            >
                              <CheckCircle2 size={15} />
                              Concluir tratamento
                            </button>
                          )}
                        </span>
                      </div>
                    )}

                    <ListaDeSessoes
                      sessoes={doTratamento}
                      vazio={
                        podeCadastrar
                          ? `Nenhuma sessão cadastrada. Use “Cadastrar sessões” e informe quantas sessões, em quais dias da semana e em que horário — o plano prevê ${tratamento.sessoesPrevistas}.`
                          : 'Nenhuma sessão cadastrada neste tratamento.'
                      }
                      {...acoesDaSessao}
                    />
                  </div>
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </Card>
  );
}
