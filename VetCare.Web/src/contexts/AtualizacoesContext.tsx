import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import axios from 'axios';
import { api } from '../services/api';
import type { EventoAtualizacao, FeedAtualizacoes } from '../types';
import { useAuth } from './auth';
import { AtualizacoesContext, type DadosAtualizacoes, type OuvinteDeAtualizacao } from './atualizacoes';

/** Segundos que a API pode segurar a requisição esperando uma novidade. */
const ESPERA_DO_SERVIDOR = 25;

/** Margem sobre a espera do servidor, para o limite ser sempre dele e não do navegador. */
const LIMITE_DA_REQUISICAO = (ESPERA_DO_SERVIDOR + 15) * 1000;

/** Intervalo entre tentativas quando a API está fora do ar. */
const PAUSA_APOS_FALHA = 5_000;

function esperar(ms: number) {
  return new Promise((resolver) => setTimeout(resolver, ms));
}

/**
 * Mantém uma única conexão com o mural de alterações da API e distribui o que chega
 * para as telas interessadas.
 *
 * O laço usa long polling: a requisição fica pendurada no servidor até haver novidade
 * ou até o prazo curto acabar, e então recomeça. O efeito prático é o de uma conexão
 * em tempo real, sem acrescentar nenhuma biblioteca ao projeto e usando o mesmo
 * cliente HTTP — com o mesmo token — do resto do sistema.
 */
export function AtualizacoesProvider({ children }: { children: ReactNode }) {
  const { usuario } = useAuth();

  const [conectado, setConectado] = useState(false);
  const [ultimoEvento, setUltimoEvento] = useState<EventoAtualizacao | null>(null);

  const ouvintes = useRef(new Set<OuvinteDeAtualizacao>());

  const assinar = useCallback((ouvinte: OuvinteDeAtualizacao) => {
    ouvintes.current.add(ouvinte);

    return () => {
      ouvintes.current.delete(ouvinte);
    };
  }, []);

  useEffect(() => {
    if (!usuario) {
      return;
    }

    let ativo = true;
    const controle = new AbortController();

    async function acompanhar() {
      // Negativo é o aperto de mão: a API responde na hora com a versão corrente, que
      // é o ponto de partida. Nada anterior a este momento interessa — a tela acabou
      // de buscar os dados por conta própria.
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
            ? [{ versao: data.versao, recurso: '*', acao: 'atualizado', descricao: '', autor: '', propria: false, em: new Date().toISOString() }]
            : data.eventos;

          desde = data.versao;

          if (eventos.length > 0) {
            const alheios = eventos.filter((evento) => !evento.propria);

            if (alheios.length > 0) {
              setUltimoEvento(alheios[alheios.length - 1]);
            }

            ouvintes.current.forEach((ouvinte) => ouvinte(eventos));
          }
        } catch (falha) {
          if (!ativo || axios.isCancel(falha)) return;

          setConectado(false);

          // Sessão encerrada: o interceptor do axios já leva ao login, aqui só paramos.
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
  }, [usuario]);

  const valor = useMemo<DadosAtualizacoes>(
    () => ({ conectado, ultimoEvento, assinar }),
    [conectado, ultimoEvento, assinar],
  );

  return <AtualizacoesContext.Provider value={valor}>{children}</AtualizacoesContext.Provider>;
}
