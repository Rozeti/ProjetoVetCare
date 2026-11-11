import {
  ClipboardList,
  FlaskConical,
  GitBranch,
  MessageCircleQuestion,
  NotebookPen,
  Pencil,
  Pill,
  Plus,
  Search,
  Stethoscope,
} from 'lucide-react';
import type { Avaliacao } from '../../types';
import { Card, Etiqueta, SemDados } from '../../components/ui';
import { estiloStatusTratamento, formatarDataHora } from '../../utils/formato';
import { Atalho, BarraDeAcoes, ItemExpansivel, Topico } from './RegistroClinico';
import { useItensAbertos } from '../../hooks/useItensAbertos';

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
  /** Leva às receitas ou aos pedidos de exame da avaliação. */
  aoVerDocumentos?: (aba: 'medicacao' | 'exames', filtro: string) => void;
}

function contar(quantidade: number, singular: string, varios: string): string {
  return `${quantidade} ${quantidade === 1 ? singular : varios}`;
}

/**
 * Avaliações do animal em linhas compactas: data, motivo da consulta e o que cada uma originou.
 * Ao expandir, aparecem os tópicos do registro (motivo, possível diagnóstico, observações), os
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
  aoVerDocumentos,
}: Props) {
  const { aberto, alternar } = useItensAbertos(emFoco);

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
          const originou = [
            avaliacao.tratamentos.length > 0 && contar(avaliacao.tratamentos.length, 'tratamento', 'tratamentos'),
            avaliacao.quantidadeReceitas > 0 && contar(avaliacao.quantidadeReceitas, 'receita', 'receitas'),
            avaliacao.quantidadePedidosExame > 0 &&
              contar(avaliacao.quantidadePedidosExame, 'pedido de exame', 'pedidos de exame'),
          ].filter(Boolean);
          const temAcoes = aoCriarTratamento || aoEmitirReceita || aoPedirExames || aoCorrigir;

          return (
            <ItemExpansivel
              key={avaliacao.id}
              ancora={`avaliacao-${avaliacao.id}`}
              aberto={aberto(avaliacao.id)}
              aoAlternar={() => alternar(avaliacao.id)}
              emFoco={avaliacao.id === emFoco}
              titulo={
                <span className="truncate font-semibold text-slate-900" title={avaliacao.motivoConsulta}>
                  {avaliacao.motivoConsulta}
                </span>
              }
              linhas={[
                [
                  formatarDataHora(avaliacao.dataRegistro),
                  avaliacao.nomeVeterinario,
                  avaliacao.dataUltimaEdicao && 'corrigida',
                  originou.length > 0 ? `originou ${originou.join(', ')}` : 'sem tratamento, receita ou exame',
                ]
                  .filter(Boolean)
                  .join(' · '),
              ]}
            >
              <div className="grid gap-4 sm:grid-cols-2">
                <Topico titulo="Motivo da consulta" icone={<MessageCircleQuestion size={14} />}>
                  {avaliacao.motivoConsulta}
                </Topico>
                <Topico titulo="Possível diagnóstico" icone={<Search size={14} />}>
                  {avaliacao.diagnosticoPossivel}
                </Topico>
              </div>

              <Topico titulo="Observações" icone={<NotebookPen size={14} />}>
                {avaliacao.observacoes}
              </Topico>

              <Topico titulo="O que a avaliação originou" icone={<GitBranch size={14} />} semCaixa>
                {originou.length === 0 ? (
                  <p className="text-sm text-slate-500">Nenhum tratamento, receita ou pedido de exame até agora.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {avaliacao.tratamentos.map((tratamento) => (
                      <Atalho
                        key={tratamento.id}
                        icone={<Stethoscope size={13} className="shrink-0 text-brand" />}
                        aoClicar={() => aoVerTratamento(tratamento.id)}
                        titulo="Abrir o tratamento"
                      >
                        {tratamento.nome}{' '}
                        <Etiqueta className={`ml-1 py-0.5 ${estiloStatusTratamento[tratamento.status] ?? 'bg-slate-200 text-slate-700'}`}>
                          {tratamento.status}
                        </Etiqueta>
                      </Atalho>
                    ))}
                    {avaliacao.quantidadeReceitas > 0 && (
                      <Atalho
                        icone={<Pill size={13} className="shrink-0 text-brand" />}
                        aoClicar={aoVerDocumentos ? () => aoVerDocumentos('medicacao', `avaliacao:${avaliacao.id}`) : undefined}
                        titulo="Ver as receitas desta avaliação"
                      >
                        {contar(avaliacao.quantidadeReceitas, 'receita', 'receitas')}
                      </Atalho>
                    )}
                    {avaliacao.quantidadePedidosExame > 0 && (
                      <Atalho
                        icone={<FlaskConical size={13} className="shrink-0 text-brand" />}
                        aoClicar={aoVerDocumentos ? () => aoVerDocumentos('exames', `avaliacao:${avaliacao.id}`) : undefined}
                        titulo="Ver os pedidos de exame desta avaliação"
                      >
                        {contar(avaliacao.quantidadePedidosExame, 'pedido de exame', 'pedidos de exame')}
                      </Atalho>
                    )}
                  </div>
                )}
              </Topico>

              {avaliacao.dataUltimaEdicao && (
                <p className="text-xs text-slate-500">Corrigida; a versão anterior fica no histórico.</p>
              )}

              {temAcoes && (
                <BarraDeAcoes>
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
                </BarraDeAcoes>
              )}
            </ItemExpansivel>
          );
        })}
      </ul>
    </Card>
  );
}
