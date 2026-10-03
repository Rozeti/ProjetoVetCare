import { createContext, useContext, useMemo, type ReactNode } from 'react';

/** O que a pergunta de confirmação mostra e como ela se comporta. */
export interface OpcoesDeConfirmacao {
  titulo: string;
  mensagem: ReactNode;
  /** Texto do botão que confirma. Padrão: "Confirmar". */
  rotuloConfirmar?: string;
  rotuloCancelar?: string;
  /** Exclusões e cancelamentos: botão vermelho e ícone de alerta. */
  perigo?: boolean;
  /** Pede um texto junto com a confirmação (ex.: motivo de um cancelamento). */
  campoTexto?: {
    rotulo: string;
    placeholder?: string;
    obrigatorio?: boolean;
  };
}

export interface RespostaDeConfirmacao {
  confirmado: boolean;
  texto: string;
}

export type Perguntar = (opcoes: OpcoesDeConfirmacao) => Promise<RespostaDeConfirmacao>;

/** Preenchido pelo ConfirmacaoProvider, que fica na raiz do portal. */
export const ContextoDeConfirmacao = createContext<Perguntar | null>(null);

/**
 * Toda exclusão, cancelamento e edição do portal pergunta antes de agir. `confirmar`
 * devolve só sim/não; `perguntar` devolve também o texto digitado, para as ações que
 * pedem um motivo.
 */
export function useConfirmacao() {
  const perguntar = useContext(ContextoDeConfirmacao);

  if (!perguntar) {
    throw new Error('useConfirmacao precisa estar dentro de ConfirmacaoProvider.');
  }

  return useMemo(
    () => ({
      perguntar,
      confirmar: async (opcoes: OpcoesDeConfirmacao) => (await perguntar(opcoes)).confirmado,
      /** Atalho para exclusões: título e botão já no tom certo. */
      confirmarExclusao: async (mensagem: ReactNode, rotuloConfirmar = 'Excluir') =>
        (await perguntar({ titulo: 'Confirmar exclusão', mensagem, rotuloConfirmar, perigo: true })).confirmado,
      /** Atalho para edições: pergunta antes de gravar por cima do registro atual. */
      confirmarEdicao: async (mensagem: ReactNode, rotuloConfirmar = 'Salvar alterações') =>
        (await perguntar({ titulo: 'Confirmar alterações', mensagem, rotuloConfirmar })).confirmado,
    }),
    [perguntar],
  );
}
