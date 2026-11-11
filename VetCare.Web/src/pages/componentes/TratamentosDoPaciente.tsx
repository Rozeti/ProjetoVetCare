import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarDays,
  CalendarPlus,
  CheckCircle2,
  ClipboardList,
  FlaskConical,
  GitBranch,
  ListFilter,
  NotebookPen,
  Pencil,
  Pill,
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
import { Atalho, BarraDeAcoes, Dado, ItemExpansivel, Topico } from './RegistroClinico';
import { useItensAbertos } from '../../hooks/useItensAbertos';

/** Quantas receitas e quantos pedidos de exame cada tratamento tem. */
export type DocumentosPorTratamento = Record<string, { receitas: number; pedidos: number }>;

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
  /** Receita e pedido de exame emitidos no tratamento; ausentes para quem não emite. */
  aoEmitirReceita?: (tratamento: Tratamento) => void;
  aoPedirExames?: (tratamento: Tratamento) => void;
  documentosPorTratamento: DocumentosPorTratamento;
  /** Leva às receitas ou aos pedidos de exame do tratamento. */
  aoVerDocumentos?: (aba: 'medicacao' | 'exames', filtro: string) => void;
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
 * expandir, aparecem os tópicos do plano, a avaliação de origem, as receitas e os pedidos de
 * exame do tratamento, as ações (emitir receita, pedir exames, editar, concluir, inativar) e as
 * sessões, onde cada sessão realizada recebe o registro do atendimento.
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
  aoEmitirReceita,
  aoPedirExames,
  documentosPorTratamento,
  aoVerDocumentos,
  aoVerAvaliacao,
  aoAtualizar,
  aoFalhar,
}: Props) {
  const { aberto, alternar } = useItensAbertos(emFoco);
  const [alterando, setAlterando] = useState('');
  const { confirmar } = useConfirmacao();

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
          const documentos = documentosPorTratamento[tratamento.id] ?? { receitas: 0, pedidos: 0 };
          const podeEmitir = ativo && (!!aoEmitirReceita || !!aoPedirExames);

          return (
            <ItemExpansivel
              key={tratamento.id}
              ancora={`tratamento-${tratamento.id}`}
              aberto={aberto(tratamento.id)}
              aoAlternar={() => alternar(tratamento.id)}
              emFoco={tratamento.id === emFoco}
              titulo={
                <>
                  <span className="truncate font-semibold text-slate-900" title={tratamento.nome}>
                    {tratamento.nome}
                  </span>
                  <Etiqueta className={estiloStatusTratamento[tratamento.status] ?? 'bg-slate-200 text-slate-700'}>
                    {tratamento.status}
                  </Etiqueta>
                </>
              }
              linhas={[
                [
                  tratamento.tipo,
                  tratamento.nomeVeterinario,
                  `${tratamento.status === 'Planejado' ? 'início previsto' : 'início'} em ${formatarData(tratamento.dataInicio)}`,
                  proxima && `próxima sessão ${formatarDataHora(proxima.dataHora)}`,
                  aguardandoRegistro > 0 &&
                    `${aguardandoRegistro} ${aguardandoRegistro === 1 ? 'sessão aguarda' : 'sessões aguardam'} registro`,
                  ativo && faltamCadastrar > 0 && `${faltamCadastrar} ${faltamCadastrar === 1 ? 'sessão' : 'sessões'} a cadastrar`,
                ]
                  .filter(Boolean)
                  .join(' · '),
              ]}
              lateral={
                <span className="hidden shrink-0 items-center gap-2 sm:flex" title="Sessões realizadas do total previsto">
                  <span className="text-xs tabular-nums text-slate-600">
                    {realizadas}/{tratamento.sessoesPrevistas}
                  </span>
                  <span className="h-1.5 w-20 overflow-hidden rounded-full bg-slate-100" aria-hidden="true">
                    <span className="block h-full rounded-full bg-brand" style={{ width: `${progresso}%` }} />
                  </span>
                </span>
              }
            >
              <Topico titulo="Plano do tratamento" icone={<ClipboardList size={14} />}>
                {tratamento.descricao}
              </Topico>

              <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <Dado rotulo="Duração prevista">{tratamento.duracaoPrevista || 'Não informada'}</Dado>
                <Dado rotulo="Frequência">{tratamento.frequencia || 'Não informada'}</Dado>
                <Dado rotulo="Sessões">
                  {tratamento.sessoesPrevistas} previstas · {cadastradas} cadastradas · {realizadas}{' '}
                  {realizadas === 1 ? 'realizada' : 'realizadas'}
                </Dado>
                <Dado rotulo={tratamento.dataFim ? 'Período' : tratamento.status === 'Planejado' ? 'Início previsto' : 'Início'}>
                  {formatarData(tratamento.dataInicio)}
                  {tratamento.dataFim && ` a ${formatarData(tratamento.dataFim)}`}
                </Dado>
              </dl>

              {tratamento.observacoesGerais && (
                <Topico titulo="Observações gerais" icone={<NotebookPen size={14} />}>
                  {tratamento.observacoesGerais}
                </Topico>
              )}

              <div className="grid gap-4 sm:grid-cols-2">
                <Topico titulo="Avaliação de origem" icone={<GitBranch size={14} />} semCaixa>
                  <Atalho
                    icone={<ClipboardList size={13} className="shrink-0 text-brand" />}
                    aoClicar={() => aoVerAvaliacao(tratamento.avaliacaoId)}
                    titulo="Abrir a avaliação"
                  >
                    Avaliação{tratamento.dataAvaliacao && ` de ${formatarData(tratamento.dataAvaliacao)}`}
                    {tratamento.motivoAvaliacao && ` — ${tratamento.motivoAvaliacao}`}
                  </Atalho>
                </Topico>

                <Topico titulo="Receitas e pedidos de exame" icone={<Pill size={14} />} semCaixa>
                  {documentos.receitas + documentos.pedidos === 0 ? (
                    <p className="text-sm text-slate-500">Nenhum emitido para este tratamento.</p>
                  ) : (
                    <div className="flex flex-wrap gap-2">
                      {documentos.receitas > 0 && (
                        <Atalho
                          icone={<Pill size={13} className="shrink-0 text-brand" />}
                          aoClicar={aoVerDocumentos ? () => aoVerDocumentos('medicacao', `tratamento:${tratamento.id}`) : undefined}
                          titulo="Ver as receitas deste tratamento"
                        >
                          {documentos.receitas} {documentos.receitas === 1 ? 'receita' : 'receitas'}
                        </Atalho>
                      )}
                      {documentos.pedidos > 0 && (
                        <Atalho
                          icone={<FlaskConical size={13} className="shrink-0 text-brand" />}
                          aoClicar={aoVerDocumentos ? () => aoVerDocumentos('exames', `tratamento:${tratamento.id}`) : undefined}
                          titulo="Ver os pedidos de exame deste tratamento"
                        >
                          {documentos.pedidos} {documentos.pedidos === 1 ? 'pedido de exame' : 'pedidos de exame'}
                        </Atalho>
                      )}
                    </div>
                  )}
                </Topico>
              </div>

              {(podeEmitir || podeGerenciar) && (
                <BarraDeAcoes>
                  {ativo && aoEmitirReceita && (
                    <button type="button" className="vc-botao-secundario" onClick={() => aoEmitirReceita(tratamento)}>
                      <Pill size={15} />
                      Emitir receita
                    </button>
                  )}
                  {ativo && aoPedirExames && (
                    <button type="button" className="vc-botao-secundario" onClick={() => aoPedirExames(tratamento)}>
                      <FlaskConical size={15} />
                      Pedir exames
                    </button>
                  )}

                  {podeGerenciar && (
                    <>
                      <button type="button" className="vc-botao-secundario" onClick={() => aoEditar(tratamento)}>
                        <Pencil size={15} />
                        Editar
                      </button>

                      {ativo && (
                        <>
                          <button
                            type="button"
                            onClick={() => mudarStatus(tratamento, 'Concluído')}
                            disabled={alterando === tratamento.id}
                            className="vc-botao-secundario"
                          >
                            <CheckCircle2 size={15} />
                            Concluir
                          </button>
                          <button
                            type="button"
                            onClick={() => mudarStatus(tratamento, 'Inativado')}
                            disabled={alterando === tratamento.id}
                            className="vc-botao-secundario text-perigo hover:bg-perigo-claro"
                          >
                            <Power size={15} />
                            Inativar
                          </button>
                        </>
                      )}

                      {tratamento.status === 'Inativado' && (
                        <button
                          type="button"
                          onClick={() => mudarStatus(tratamento, 'Em Andamento')}
                          disabled={alterando === tratamento.id}
                          className="vc-botao-secundario text-sucesso hover:bg-sucesso-claro"
                        >
                          <RotateCcw size={15} />
                          Reativar
                        </button>
                      )}
                    </>
                  )}
                </BarraDeAcoes>
              )}

              {/* div, e não section: os blocos de ListaDeSessoes já são as seções das sessões. */}
              <div className="border-t border-slate-100 pt-4">
                <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h4 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                      <span className="flex h-6 w-6 items-center justify-center rounded-md bg-brand-50 text-brand" aria-hidden="true">
                        <CalendarDays size={14} />
                      </span>
                      Sessões do tratamento
                    </h4>
                    <p className="mt-0.5 text-xs text-slate-500">
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
            </ItemExpansivel>
          );
        })}
      </ul>
    </Card>
  );
}
