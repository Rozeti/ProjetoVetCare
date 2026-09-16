import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { PaginaDe } from '../types';

interface Props<T> {
  pagina: PaginaDe<T>;
  aoMudarPagina: (pagina: number) => void;
  rotuloItens?: string;
}

/**
 * Controle de paginação das listagens. A API limita o tamanho da página, então a
 * navegação é sempre necessária quando a base cresce.
 */
export function Paginacao<T>({ pagina, aoMudarPagina, rotuloItens = 'registros' }: Props<T>) {
  if (pagina.total === 0) {
    return null;
  }

  const primeiro = (pagina.pagina - 1) * pagina.tamanho + 1;
  const ultimo = Math.min(pagina.pagina * pagina.tamanho, pagina.total);

  // Com muitas páginas, mostramos apenas uma janela ao redor da atual.
  const paginas = montarJanela(pagina.pagina, pagina.totalDePaginas);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3">
      <p className="text-sm text-slate-500">
        Exibindo <strong className="text-slate-700">{primeiro}</strong>–
        <strong className="text-slate-700">{ultimo}</strong> de{' '}
        <strong className="text-slate-700">{pagina.total}</strong> {rotuloItens}
      </p>

      {pagina.totalDePaginas > 1 && (
        <nav className="flex items-center gap-1" aria-label="Paginação">
          <button
            type="button"
            onClick={() => aoMudarPagina(pagina.pagina - 1)}
            disabled={!pagina.temAnterior}
            className="rounded-lg p-2 text-slate-600 transition hover:bg-slate-100 disabled:opacity-40"
            aria-label="Página anterior"
          >
            <ChevronLeft size={16} />
          </button>

          {paginas.map((numero, indice) =>
            numero === null ? (
              <span key={`separador-${indice}`} className="px-1.5 text-slate-400">
                …
              </span>
            ) : (
              <button
                key={numero}
                type="button"
                onClick={() => aoMudarPagina(numero)}
                aria-current={numero === pagina.pagina ? 'page' : undefined}
                className={`min-w-9 rounded-lg px-2.5 py-1.5 text-sm font-medium transition ${
                  numero === pagina.pagina
                    ? 'bg-brand text-white'
                    : 'text-slate-600 hover:bg-slate-100'
                }`}
              >
                {numero}
              </button>
            ),
          )}

          <button
            type="button"
            onClick={() => aoMudarPagina(pagina.pagina + 1)}
            disabled={!pagina.temProxima}
            className="rounded-lg p-2 text-slate-600 transition hover:bg-slate-100 disabled:opacity-40"
            aria-label="Próxima página"
          >
            <ChevronRight size={16} />
          </button>
        </nav>
      )}
    </div>
  );
}

/** Primeira, última e as vizinhas da atual; o resto vira reticências. */
function montarJanela(atual: number, total: number): (number | null)[] {
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  const numeros = new Set<number>([1, total, atual]);

  if (atual > 1) numeros.add(atual - 1);
  if (atual < total) numeros.add(atual + 1);

  const ordenados = [...numeros].sort((a, b) => a - b);
  const resultado: (number | null)[] = [];

  ordenados.forEach((numero, indice) => {
    if (indice > 0 && numero - ordenados[indice - 1] > 1) {
      resultado.push(null);
    }

    resultado.push(numero);
  });

  return resultado;
}
