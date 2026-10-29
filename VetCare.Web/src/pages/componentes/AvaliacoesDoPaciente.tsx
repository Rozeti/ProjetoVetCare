import { ClipboardList, FlaskConical, Pencil, Pill, Plus, Stethoscope } from 'lucide-react';
import type { Avaliacao } from '../../types';
import { Card, Etiqueta, SemDados } from '../../components/ui';
import { estiloStatusTratamento, formatarDataHora } from '../../utils/formato';

interface Props {
  avaliacoes: Avaliacao[];
  /** Avaliação em destaque, vinda de um atalho da linha do tempo ou de um tratamento. */
  emFoco?: string;
  /** Ações clínicas; ausentes para quem só consulta ou com o prontuário encerrado. */
  aoRegistrar?: () => void;
  aoCriarTratamento?: (avaliacao: Avaliacao) => void;
  aoEmitirReceita?: (avaliacao: Avaliacao) => void;
  aoPedirExames?: (avaliacao: Avaliacao) => void;
  aoCorrigir?: (avaliacao: Avaliacao) => void;
  aoVerTratamento: (tratamentoId: string) => void;
}

/**
 * Avaliações do animal. Cada uma registra o motivo da consulta, as observações, o possível
 * diagnóstico e o veterinário responsável, e é o ponto de partida dos tratamentos, receitas
 * e pedidos de exame.
 */
export function AvaliacoesDoPaciente({
  avaliacoes,
  emFoco,
  aoRegistrar,
  aoCriarTratamento,
  aoEmitirReceita,
  aoPedirExames,
  aoCorrigir,
  aoVerTratamento,
}: Props) {
  if (avaliacoes.length === 0) {
    return (
      <Card>
        <SemDados
          icone={<ClipboardList size={40} />}
          titulo="Nenhuma avaliação registrada"
          descricao="A avaliação é o primeiro registro clínico da consulta. É a partir dela que se criam tratamentos, receitas e pedidos de exame."
          acao={
            aoRegistrar ? (
              <button type="button" className="vc-botao-sutil" onClick={aoRegistrar}>
                <Plus size={16} />
                Registrar avaliação
              </button>
            ) : undefined
          }
        />
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {avaliacoes.map((avaliacao) => (
        <Card
          key={avaliacao.id}
          className={`p-5 ${emFoco === avaliacao.id ? 'ring-2 ring-brand-300' : ''}`}
        >
          <div id={`avaliacao-${avaliacao.id}`} className="mb-3 flex scroll-mt-24 flex-wrap items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-slate-900">Avaliação de {formatarDataHora(avaliacao.dataRegistro)}</h3>
              <p className="mt-0.5 text-xs text-slate-500">
                {avaliacao.nomeVeterinario}
                {avaliacao.dataUltimaEdicao && ' · corrigida (versão anterior no histórico)'}
              </p>
            </div>

            {aoCorrigir && (
              <button
                type="button"
                onClick={() => aoCorrigir(avaliacao)}
                className="inline-flex items-center gap-1 text-sm font-medium text-slate-500 hover:text-brand"
                title="Corrigir esta avaliação; a versão anterior fica no histórico"
              >
                <Pencil size={14} />
                Corrigir
              </button>
            )}
          </div>

          <dl className="grid gap-3 sm:grid-cols-2">
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Motivo da consulta</dt>
              <dd className="mt-0.5 whitespace-pre-line text-sm text-slate-800">{avaliacao.motivoConsulta}</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Possível diagnóstico</dt>
              <dd className="mt-0.5 whitespace-pre-line text-sm text-slate-800">{avaliacao.diagnosticoPossivel}</dd>
            </div>
            <div className="sm:col-span-2">
              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Observações</dt>
              <dd className="mt-0.5 whitespace-pre-line text-sm text-slate-700">{avaliacao.observacoes}</dd>
            </div>
          </dl>

          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3 text-xs text-slate-500">
            <span className="font-semibold text-slate-600">Originou:</span>
            {avaliacao.tratamentos.length === 0 && avaliacao.quantidadeReceitas === 0 && avaliacao.quantidadePedidosExame === 0 && (
              <span>nenhum tratamento, receita ou pedido de exame</span>
            )}
            {avaliacao.tratamentos.map((tratamento) => (
              <button
                key={tratamento.id}
                type="button"
                onClick={() => aoVerTratamento(tratamento.id)}
                className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-1 font-medium text-slate-700 hover:bg-slate-200"
                title="Abrir o tratamento"
              >
                <Stethoscope size={12} />
                {tratamento.nome}
                <Etiqueta className={estiloStatusTratamento[tratamento.status] ?? 'bg-slate-200 text-slate-700'}>
                  {tratamento.status}
                </Etiqueta>
              </button>
            ))}
            {avaliacao.quantidadeReceitas > 0 && (
              <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1">
                <Pill size={12} />
                {avaliacao.quantidadeReceitas} {avaliacao.quantidadeReceitas === 1 ? 'receita' : 'receitas'}
              </span>
            )}
            {avaliacao.quantidadePedidosExame > 0 && (
              <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1">
                <FlaskConical size={12} />
                {avaliacao.quantidadePedidosExame}{' '}
                {avaliacao.quantidadePedidosExame === 1 ? 'pedido de exame' : 'pedidos de exame'}
              </span>
            )}
          </div>

          {(aoCriarTratamento || aoEmitirReceita || aoPedirExames) && (
            <div className="mt-3 flex flex-wrap gap-2">
              {aoCriarTratamento && (
                <button type="button" className="vc-botao-secundario" onClick={() => aoCriarTratamento(avaliacao)}>
                  <Stethoscope size={15} />
                  Criar tratamento
                </button>
              )}
              {aoEmitirReceita && (
                <button type="button" className="vc-botao-secundario" onClick={() => aoEmitirReceita(avaliacao)}>
                  <Pill size={15} />
                  Emitir receita
                </button>
              )}
              {aoPedirExames && (
                <button type="button" className="vc-botao-secundario" onClick={() => aoPedirExames(avaliacao)}>
                  <FlaskConical size={15} />
                  Pedir exames
                </button>
              )}
            </div>
          )}
        </Card>
      ))}
    </div>
  );
}
