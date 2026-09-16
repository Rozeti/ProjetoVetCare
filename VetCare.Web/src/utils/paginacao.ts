import type { PaginaDe } from '../types';

/**
 * Envelope vazio usado enquanto a primeira página não chegou da API, para que a tela
 * possa ler `pagina.itens` e `pagina.total` sem verificar nulo em cada uso.
 */
export function paginaVazia<T>(tamanho = 20): PaginaDe<T> {
  return {
    itens: [],
    pagina: 1,
    tamanho,
    total: 0,
    totalDePaginas: 0,
    temAnterior: false,
    temProxima: false,
  };
}
