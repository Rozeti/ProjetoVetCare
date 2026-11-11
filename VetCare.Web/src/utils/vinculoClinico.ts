import type { Avaliacao, TipoOrigem, Tratamento } from '../types';
import { formatarData, resumirTexto } from './formato';

/*
 * Origem de receitas e pedidos de exame: a avaliação em que foram feitos ou o tratamento —
 * direto nele ou no registro de uma das sessões dele. A sessão fica guardada no registro, mas a
 * origem mostrada é o tratamento: a sessão está dentro dele.
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
export type OrigemClinica = { avaliacaoId: string } | { tratamentoId: string } | { atendimentoId: string };

/** O que o veterinário emite a partir de uma sessão. */
export type TipoDeEmissao = 'receita' | 'exame';

/** Aba do prontuário onde cada tipo de documento aparece depois de emitido. */
export const ABA_DA_EMISSAO: Record<TipoDeEmissao, 'medicacao' | 'exames'> = {
  receita: 'medicacao',
  exame: 'exames',
};

/** Valor do campo de origem: "avaliacao:<id>", "tratamento:<id>" ou "sessao:<id>". */
export function valorDaOrigem(origem?: OrigemClinica | null): string {
  if (!origem) return '';
  if ('avaliacaoId' in origem) return `avaliacao:${origem.avaliacaoId}`;
  if ('tratamentoId' in origem) return `tratamento:${origem.tratamentoId}`;
  return `sessao:${origem.atendimentoId}`;
}

/** Ids da origem escolhida, no formato que a API espera. */
export function lerOrigem(valor: string): {
  avaliacaoId: string | null;
  tratamentoId: string | null;
  atendimentoId: string | null;
} {
  const [tipo, id] = valor.split(':');
  return {
    avaliacaoId: tipo === 'avaliacao' ? id : null,
    tratamentoId: tipo === 'tratamento' ? id : null,
    atendimentoId: tipo === 'sessao' ? id : null,
  };
}

/** "Avaliação de 10/11/2026 — Claudicação no membro posterior". */
export function descreverAvaliacao(avaliacao: Pick<Avaliacao, 'dataRegistro' | 'motivoConsulta'>, limite = 60): string {
  return `Avaliação de ${formatarData(avaliacao.dataRegistro)} — ${resumirTexto(avaliacao.motivoConsulta, limite)}`;
}

/** A origem escolhida no formulário, por extenso; vazio se não achar. */
export function descreverOrigem(
  valor: string,
  avaliacoes: Avaliacao[],
  sessoes: SessaoRegistrada[],
  tratamentos: Tratamento[],
): string {
  const { avaliacaoId, tratamentoId, atendimentoId } = lerOrigem(valor);

  if (avaliacaoId) {
    const avaliacao = avaliacoes.find((a) => a.id === avaliacaoId);
    return avaliacao ? descreverAvaliacao(avaliacao) : '';
  }

  if (tratamentoId) {
    const tratamento = tratamentos.find((t) => t.id === tratamentoId);
    return tratamento ? `Tratamento ${tratamento.nome}` : '';
  }

  // Feito no registro de uma sessão: a origem é o tratamento dela.
  const sessao = sessoes.find((s) => s.id === atendimentoId);
  return sessao ? `Tratamento ${sessao.nomeTratamento}` : '';
}

const ATIVOS: Tratamento['status'][] = ['Planejado', 'Em Andamento'];

/** Tratamentos que recebem receitas e pedidos novos: os planejados e os em andamento. */
export function tratamentosAtivos(tratamentos: Tratamento[]): Tratamento[] {
  return tratamentos.filter((t) => ATIVOS.includes(t.status));
}

/** O que as listas de receitas e de pedidos sabem sobre a origem de cada item. */
export interface ComOrigem {
  tipoOrigem: TipoOrigem;
  avaliacaoId?: string | null;
  dataAvaliacao?: string | null;
  motivoAvaliacao: string;
  tratamentoId?: string | null;
  nomeTratamento: string;
}

export const SEM_ORIGEM = 'sem-origem';

export interface OpcaoDeOrigem {
  valor: string;
  rotulo: string;
  grupo: 'Avaliações' | 'Tratamentos' | 'Outros';
}

/**
 * Opções do filtro das abas Medicação e Exames: as avaliações e os tratamentos que aparecem na
 * lista e, se houver, os registros antigos sem origem.
 */
export function opcoesDeOrigem(itens: ComOrigem[]): OpcaoDeOrigem[] {
  const avaliacoes = new Map<string, OpcaoDeOrigem>();
  const tratamentos = new Map<string, OpcaoDeOrigem>();
  let haSemOrigem = false;

  for (const item of itens) {
    if (item.tipoOrigem === 'Avaliação' && item.avaliacaoId) {
      avaliacoes.set(item.avaliacaoId, {
        valor: `avaliacao:${item.avaliacaoId}`,
        rotulo: item.dataAvaliacao
          ? descreverAvaliacao({ dataRegistro: item.dataAvaliacao, motivoConsulta: item.motivoAvaliacao }, 40)
          : 'Avaliação',
        grupo: 'Avaliações',
      });
    } else if (!item.tipoOrigem) {
      haSemOrigem = true;
    }

    // Um tratamento entra no filtro também quando é só o vínculo de um registro antigo da avaliação.
    if (item.tratamentoId) {
      tratamentos.set(item.tratamentoId, {
        valor: `tratamento:${item.tratamentoId}`,
        rotulo: item.nomeTratamento || 'Tratamento',
        grupo: 'Tratamentos',
      });
    }
  }

  const porRotulo = (a: OpcaoDeOrigem, b: OpcaoDeOrigem) => a.rotulo.localeCompare(b.rotulo, 'pt-BR');

  return [
    ...[...avaliacoes.values()],
    ...[...tratamentos.values()].sort(porRotulo),
    ...(haSemOrigem ? [{ valor: SEM_ORIGEM, rotulo: 'Sem origem registrada', grupo: 'Outros' as const }] : []),
  ];
}

/** O item pertence à origem do filtro? O tratamento inclui o que foi feito nele e o vinculado a ele. */
export function pertenceAOrigem(item: ComOrigem, filtro: string): boolean {
  if (filtro === SEM_ORIGEM) return !item.tipoOrigem;

  const [tipo, id] = filtro.split(':');
  if (tipo === 'avaliacao') return item.tipoOrigem === 'Avaliação' && item.avaliacaoId === id;
  if (tipo === 'tratamento') return item.tratamentoId === id;
  return true;
}

/**
 * Aplica o filtro de origem. Um filtro que deixou de existir na lista (o último item dele foi
 * excluído, por exemplo) volta a mostrar tudo, em vez de esconder a lista inteira.
 */
export function filtrarPorOrigem<T extends ComOrigem>(itens: T[], filtro: string): T[] {
  if (!filtro || !opcoesDeOrigem(itens).some((opcao) => opcao.valor === filtro)) return itens;

  return itens.filter((item) => pertenceAOrigem(item, filtro));
}
