import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { AppState } from 'react-native';
import axios from 'axios';
import { api } from '../services/api';
import type { EventoAtualizacao, FeedAtualizacoes, RecursoAtualizado } from '../tipos';
import { useAuth } from './AuthContext';

/** Segundos que a API pode segurar a requisição esperando uma novidade. */
const ESPERA_DO_SERVIDOR = 25;

/** Margem sobre a espera do servidor: o prazo tem de ser sempre dele, não do aparelho. */
const LIMITE_DA_REQUISICAO = (ESPERA_DO_SERVIDOR + 15) * 1000;

/** Intervalo entre tentativas quando o celular está sem rede ou a API está fora. */
const PAUSA_APOS_FALHA = 5_000;

type OuvinteDeAtualizacao = (eventos: EventoAtualizacao[]) => void;

interface DadosAtualizacoes {
  /** Falso enquanto a conexão com o mural está caída. */
  conectado: boolean;
  assinar: (ouvinte: OuvinteDeAtualizacao) => () => void;
}

const AtualizacoesContext = createContext<DadosAtualizacoes>({
  conectado: false,
  assinar: () => () => {},
});

function esperar(ms: number) {
  return new Promise((resolver) => setTimeout(resolver, ms));
}

/**
 * Mantém uma única conexão com o mural de alterações da API e avisa as telas abertas.
 *
 * A técnica é long polling: a requisição fica pendurada no servidor até haver novidade
 * ou até o prazo curto acabar, e então recomeça. O efeito é o de uma conexão em tempo
 * real sem acrescentar dependência nativa nenhuma ao projeto — o que manteria o
 * aplicativo fora do Expo Go usado no desenvolvimento.
 *
 * O laço acompanha o estado do aplicativo: em segundo plano ele para, e ao voltar para
 * a frente recomeça avisando as telas, que então recarregam o que perderam.
 */
export function AtualizacoesProvider({ children }: { children: ReactNode }) {
  const { usuario } = useAuth();

  const [conectado, setConectado] = useState(false);
  const [emPrimeiroPlano, setEmPrimeiroPlano] = useState(AppState.currentState === 'active');

  const ouvintes = useRef(new Set<OuvinteDeAtualizacao>());

  const assinar = useCallback((ouvinte: OuvinteDeAtualizacao) => {
    ouvintes.current.add(ouvinte);

    return () => {
      ouvintes.current.delete(ouvinte);
    };
  }, []);

  useEffect(() => {
    const inscricao = AppState.addEventListener('change', (estado) => {
      setEmPrimeiroPlano(estado === 'active');
    });

    return () => inscricao.remove();
  }, []);

  useEffect(() => {
    if (!usuario || !emPrimeiroPlano) {
      return;
    }

    let ativo = true;
    const controle = new AbortController();

    async function acompanhar() {
      // Negativo é o aperto de mão: a API responde na hora com a versão corrente, que
      // é o ponto de partida. Nada anterior interessa — a tela acabou de carregar.
      let desde = -1;

      while (ativo) {
        try {
          const { data } = await api.get<FeedAtualizacoes>('/api/atualizacoes', {
            params: { desde, espera: ESPERA_DO_SERVIDOR },
            timeout: LIMITE_DA_REQUISICAO,
            signal: controle.signal,
          });

          if (!ativo) return;

          setConectado(true);

          // Ficamos fora tempo demais e perdemos eventos: em vez de aplicar um retrato
          // incompleto, pedimos a todas as telas que recarreguem.
          const eventos: EventoAtualizacao[] = data.reiniciar
            ? [
                {
                  versao: data.versao,
                  recurso: '*',
                  acao: 'atualizado',
                  descricao: '',
                  autor: '',
                  propria: false,
                  em: new Date().toISOString(),
                },
              ]
            : data.eventos;

          desde = data.versao;

          if (eventos.length > 0) {
            ouvintes.current.forEach((ouvinte) => ouvinte(eventos));
          }
        } catch (falha) {
          if (!ativo || axios.isCancel(falha)) return;

          setConectado(false);

          // Sessão encerrada: não adianta insistir, o app volta para o login.
          if (axios.isAxiosError(falha) && falha.response?.status === 401) return;

          await esperar(PAUSA_APOS_FALHA);
        }
      }
    }

    acompanhar();

    return () => {
      ativo = false;
      controle.abort();
      setConectado(false);
    };
  }, [usuario, emPrimeiroPlano]);

  const valor = useMemo<DadosAtualizacoes>(() => ({ conectado, assinar }), [conectado, assinar]);

  return <AtualizacoesContext.Provider value={valor}>{children}</AtualizacoesContext.Provider>;
}

export function useAtualizacoes() {
  return useContext(AtualizacoesContext);
}

/**
 * Executa `aoMudar` sempre que a API avisar que um dos `recursos` mudou no banco.
 * É o que faz a agenda do tutor refletir na hora um cancelamento feito na clínica, e a
 * lista de pets mostrar sozinha o animal que a equipe acabou de cadastrar.
 */
export function useAtualizacao(recursos: RecursoAtualizado[], aoMudar: () => void) {
  const { assinar } = useAtualizacoes();

  const recursosRef = useRef(recursos);
  const acaoRef = useRef(aoMudar);

  // Atualizadas após a renderização, para a assinatura enxergar sempre a versão mais
  // recente sem precisar ser refeita a cada render.
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
