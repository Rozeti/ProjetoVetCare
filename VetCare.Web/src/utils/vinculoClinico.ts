import type { Avaliacao, Tratamento } from '../types';
import { formatarData, resumirTexto } from './formato';

/*
 * Origem e tratamento de receitas e pedidos de exame. Os dois nascem de uma avaliação ou de uma
 * sessão registrada e podem ficar ligados ao tratamento para o qual foram feitos: na sessão, o
 * tratamento é sempre o dela; na avaliação, o vínculo é opcional e só com tratamento ativo.
 */

/** Sessão registrada (o atendimento) que pode originar uma receita ou um pedido de exame. */
export interface SessaoRegistrada {
  /** Id do atendimento que registrou a sessão. */
  id: string;
  data: string;
  numeroSessao: number;
  tratamentoId: string;
  nomeTratamento: string;
  /** Quem atendeu: assina o pedido de exame quando a administração o emite. */
  nomeVeterinario?: string;
}

/** Sessão registrada de onde partem uma receita ou um pedido, com o animal a que pertence. */
export interface SessaoDeOrigem extends SessaoRegistrada {
  pacienteId: string;
  nomePaciente: string;
}

/** Origem já decidida pelo botão que abriu o formulário. */
export type OrigemClinica = { avaliacaoId: string } | { atendimentoId: string };

/** O que o veterinário emite a partir de uma sessão. */
export type TipoDeEmissao = 'receita' | 'exame';

/** Aba do prontuário onde cada tipo de documento aparece depois de emitido. */
export const ABA_DA_EMISSAO: Record<TipoDeEmissao, 'medicacao' | 'exames'> = {
  receita: 'medicacao',
  exame: 'exames',
};

/** Valor do campo de origem: "avaliacao:<id>" ou "sessao:<id>". */
export function valorDaOrigem(origem?: OrigemClinica | null): string {
  if (!origem) return '';
  return 'avaliacaoId' in origem ? `avaliacao:${origem.avaliacaoId}` : `sessao:${origem.atendimentoId}`;
}

/** Ids da origem escolhida, no formato que a API espera. */
export function lerOrigem(valor: string): { avaliacaoId: string | null; atendimentoId: string | null } {
  const [tipo, id] = valor.split(':');
  return {
    avaliacaoId: tipo === 'avaliacao' ? id : null,
    atendimentoId: tipo === 'sessao' ? id : null,
  };
}

export function descreverSessao(sessao: SessaoRegistrada): string {
  return `Sessão nº ${sessao.numeroSessao} de ${formatarData(sessao.data)}`;
}

/** "Avaliação de 10/11/2026 — motivo" ou "Sessão nº 3 de 12/11/2026"; vazio se não achar. */
export function descreverOrigem(valor: string, avaliacoes: Avaliacao[], sessoes: SessaoRegistrada[]): string {
  const { avaliacaoId, atendimentoId } = lerOrigem(valor);

  if (avaliacaoId) {
    const avaliacao = avaliacoes.find((a) => a.id === avaliacaoId);
    return avaliacao
      ? `Avaliação de ${formatarData(avaliacao.dataRegistro)} — ${resumirTexto(avaliacao.motivoConsulta, 60)}`
      : '';
  }

  const sessao = sessoes.find((s) => s.id === atendimentoId);
  return sessao ? descreverSessao(sessao) : '';
}

const ATIVOS: Tratamento['status'][] = ['Planejado', 'Em Andamento'];

/** Tratamentos que aceitam um vínculo novo (os ativos), mais o já ligado ao registro em correção. */
export function tratamentosParaVinculo(tratamentos: Tratamento[], atual?: string | null): Tratamento[] {
  return tratamentos.filter((t) => ATIVOS.includes(t.status) || t.id === atual);
}

/** Sugestão para a origem avaliação: o único tratamento ativo que ela originou, se houver um só. */
export function tratamentoSugerido(tratamentos: Tratamento[], valorDaOrigemEscolhida: string): string {
  const { avaliacaoId } = lerOrigem(valorDaOrigemEscolhida);
  if (!avaliacaoId) return '';

  const daAvaliacao = tratamentosParaVinculo(tratamentos).filter((t) => t.avaliacaoId === avaliacaoId);
  return daAvaliacao.length === 1 ? daAvaliacao[0].id : '';
}

/** O que as listas de receitas e de pedidos sabem sobre o tratamento de cada item. */
export interface ComTratamento {
  tratamentoId?: string | null;
  nomeTratamento: string;
}

export const SEM_TRATAMENTO = 'sem-tratamento';

/** Opções do filtro: os tratamentos presentes na lista e, se houver, os itens sem tratamento. */
export function opcoesDeTratamento(itens: ComTratamento[]): { valor: string; rotulo: string }[] {
  const tratamentos = new Map<string, string>();
  let haSemTratamento = false;

  for (const item of itens) {
    if (item.tratamentoId) tratamentos.set(item.tratamentoId, item.nomeTratamento || 'Tratamento');
    else haSemTratamento = true;
  }

  const opcoes = Array.from(tratamentos, ([valor, rotulo]) => ({ valor, rotulo })).sort((a, b) =>
    a.rotulo.localeCompare(b.rotulo, 'pt-BR'),
  );

  return haSemTratamento ? [...opcoes, { valor: SEM_TRATAMENTO, rotulo: 'Sem tratamento vinculado' }] : opcoes;
}

/**
 * Aplica o filtro de tratamento. Um filtro que deixou de existir na lista (o último item dele
 * foi excluído, por exemplo) volta a mostrar tudo, em vez de esconder a lista inteira.
 */
export function filtrarPorTratamento<T extends ComTratamento>(itens: T[], filtro: string): T[] {
  const opcoes = opcoesDeTratamento(itens);

  if (!filtro || opcoes.length < 2 || !opcoes.some((opcao) => opcao.valor === filtro)) return itens;

  return itens.filter((item) => (filtro === SEM_TRATAMENTO ? !item.tratamentoId : item.tratamentoId === filtro));
}
