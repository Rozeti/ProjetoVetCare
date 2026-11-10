import { useState } from 'react';
import { ChevronDown, ChevronRight, ClipboardList, FlaskConical, Pencil, Pill, Plus, Stethoscope } from 'lucide-react';
import type { Avaliacao } from '../../types';
import { Card, Etiqueta, SemDados } from '../../components/ui';
import { estiloStatusTratamento, formatarDataHora } from '../../utils/formato';

interface Props {
  avaliacoes: Avaliacao[];
  /** Avaliação em destaque, vinda de um atalho da linha do tempo ou de um tratamento: abre expandida. */
  emFoco?: string;
  /** Ações clínicas; ausentes para quem só consulta ou com o prontuário encerrado. */
  aoRegistrar?: () => void;
  aoCriarTratamento?: (avaliacao: Avaliacao) => void;
  aoEmitirReceita?: (avaliacao: Avaliacao) => void;
  aoPedirExames?: (avaliacao: Avaliacao) => void;
  aoCorrigir?: (avaliacao: Avaliacao) => void;
  aoVerTratamento: (tratamentoId: string) => void;
}

function contar(quantidade: number, singular: string, varios: string): string {
  return `${quantidade} ${quantidade === 1 ? singular : varios}`;
}

/**
 * Avaliações do animal em linhas compactas: data, motivo da consulta e o que cada uma originou.
 * Ao expandir, aparecem o registro completo (motivo, possível diagnóstico, observações), os
 * tratamentos, receitas e pedidos de exame originados e as ações a partir da avaliação.
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
  // A avaliação em foco abre expandida; clicar alterna a partir daí.
  const [alternadas, setAlternadas] = useState<Set<string>>(new Set());

  function alternar(id: string) {
    setAlternadas((atual) => {
      const nova = new Set(atual);
      if (nova.has(id)) nova.delete(id);
      else nova.add(id);
      return nova;
    });
  }

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
    <Card className="overflow-hidden">
      <ul className="divide-y divide-slate-100">
        {avaliacoes.map((avaliacao) => {
          const aberta = (avaliacao.id === emFoco) !== alternadas.has(avaliacao.id);
          const originou = [
            avaliacao.tratamentos.length > 0 && contar(avaliacao.tratamentos.length, 'tratamento', 'tratamentos'),
            avaliacao.quantidadeReceitas > 0 && contar(avaliacao.quantidadeReceitas, 'receita', 'receitas'),
            avaliacao.quantidadePedidosExame > 0 &&
              contar(avaliacao.quantidadePedidosExame, 'pedido de exame', 'pedidos de exame'),
          ].filter(Boolean);

          return (
            <li
              key={avaliacao.id}
              id={`avaliacao-${avaliacao.id}`}
              className={`scroll-mt-24 ${avaliacao.id === emFoco ? 'bg-brand-50/40' : ''}`}
            >
              <button
                type="button"
                onClick={() => alternar(avaliacao.id)}
                aria-expanded={aberta}
                className="flex w-full items-center gap-3 px-4 py-3 text-left transition hover:bg-slate-50 sm:px-5"
              >
                {aberta ? (
                  <ChevronDown size={18} className="shrink-0 text-slate-400" aria-hidden="true" />
                ) : (
                  <ChevronRight size={18} className="shrink-0 text-slate-400" aria-hidden="true" />
                )}

                <span className="min-w-0 flex-1">
                  <span className="block truncate font-semibold text-slate-900" title={avaliacao.motivoConsulta}>
                    {avaliacao.motivoConsulta}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-slate-500">
                    {[
                      formatarDataHora(avaliacao.dataRegistro),
                      avaliacao.nomeVeterinario,
                      avaliacao.dataUltimaEdicao && 'corrigida',
                      originou.length > 0 ? `originou ${originou.join(', ')}` : 'sem tratamento, receita ou exame',
                    ]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                </span>
              </button>

              {aberta && (
                <div className="space-y-4 border-t border-slate-100 px-4 pb-5 pt-4 sm:px-5 sm:pl-12">
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

                  {avaliacao.dataUltimaEdicao && (
                    <p className="text-xs text-slate-500">Corrigida; a versão anterior fica no histórico.</p>
                  )}

                  {avaliacao.tratamentos.length > 0 && (
                    <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                      <span className="font-semibold text-slate-600">Tratamentos:</span>
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
                    </div>
                  )}

                  {(aoCriarTratamento || aoEmitirReceita || aoPedirExames || aoCorrigir) && (
                    <div className="flex flex-wrap gap-2">
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
                      {aoCorrigir && (
                        <button
                          type="button"
                          className="vc-botao-secundario"
                          onClick={() => aoCorrigir(avaliacao)}
                          title="Corrigir esta avaliação; a versão anterior fica no histórico"
                        >
                          <Pencil size={15} />
                          Corrigir
                        </button>
                      )}
                    </div>
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </Card>
  );
}
