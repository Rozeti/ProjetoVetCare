import { createContext, useContext, useEffect, useRef } from 'react';
import type { EventoAtualizacao, RecursoAtualizado } from '../types';

export type OuvinteDeAtualizacao = (eventos: EventoAtualizacao[]) => void;

export interface DadosAtualizacoes {
  /** Falso enquanto a conexão com o mural está caída; o Layout mostra isso ao usuário. */
  conectado: boolean;
  /** Último evento recebido, usado para o aviso discreto no topo da tela. */
  ultimoEvento: EventoAtualizacao | null;
  /** Registra um ouvinte e devolve a função que o remove. */
  assinar: (ouvinte: OuvinteDeAtualizacao) => () => void;
}

/**
 * O contexto e os hooks moram fora do arquivo do provedor para que aquele arquivo
 * exporte apenas componentes — condição para o hot reload do Vite preservar o estado.
 */
export const AtualizacoesContext = createContext<DadosAtualizacoes>({
  conectado: false,
  ultimoEvento: null,
  assinar: () => () => {},
});

export function useAtualizacoes() {
  return useContext(AtualizacoesContext);
}

/**
 * Executa `aoMudar` sempre que a API avisar que um dos `recursos` mudou no banco —
 * não importa quem mexeu nem de qual tela. É assim que a confirmação de presença
 * feita pelo tutor no celular reaparece na agenda do veterinário, do apoio e do
 * administrativo sem ninguém apertar "atualizar".
 *
 * `recursos` é lido a cada evento, então pode ser um array literal na chamada; o
 * mesmo vale para `aoMudar`, guardado numa referência para não reiniciar a assinatura
 * a cada renderização.
 */
export function useAtualizacao(recursos: RecursoAtualizado[], aoMudar: () => void) {
  const { assinar } = useAtualizacoes();

  const recursosRef = useRef(recursos);
  const acaoRef = useRef(aoMudar);

  // As referências são atualizadas depois da renderização para que a assinatura
  // continue enxergando sempre a versão mais recente sem ser refeita a cada render.
  useEffect(() => {
    recursosRef.current = recursos;
    acaoRef.current = aoMudar;
  });

  useEffect(() => {
    return assinar((eventos) => {
      // '*' é o pedido de recarga geral emitido quando a conexão ficou para trás.
      if (eventos.some((e) => e.recurso === '*' || recursosRef.current.includes(e.recurso))) {
        acaoRef.current();
      }
    });
  }, [assinar]);
}
