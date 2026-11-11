import type { ReactNode } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';

/*
 * Peças das listas clínicas do prontuário (avaliações, tratamentos, receitas e pedidos de
 * exame): o item que se expande e os tópicos do registro aberto, com o título destacado para
 * que cada informação seja achada de relance.
 */

interface PropsDoItem {
  /** Âncora do item, para os atalhos que levam até ele. */
  ancora: string;
  aberto: boolean;
  aoAlternar: () => void;
  /** Destaque do item que chegou por um atalho. */
  emFoco?: boolean;
  /** Primeira linha: o nome do registro e as etiquetas. */
  titulo: ReactNode;
  /** Linhas de apoio, em texto menor, abaixo do título. */
  linhas?: ReactNode[];
  /** Informação à direita, dentro da área clicável (ex.: andamento). */
  lateral?: ReactNode;
  /** Botões fora da área clicável, sempre à vista (ex.: imprimir). */
  atalhos?: ReactNode;
  /** Rótulo acessível do botão que expande, quando o título não basta. */
  rotulo?: string;
  children: ReactNode;
}

/** Linha compacta que expande o registro completo logo abaixo. */
export function ItemExpansivel({
  ancora,
  aberto,
  aoAlternar,
  emFoco = false,
  titulo,
  linhas = [],
  lateral,
  atalhos,
  rotulo,
  children,
}: PropsDoItem) {
  const Seta = aberto ? ChevronDown : ChevronRight;

  return (
    <li id={ancora} className={`scroll-mt-24 ${emFoco ? 'bg-brand-50/40' : ''}`}>
      <div className={`flex items-center gap-1 pr-2 transition hover:bg-slate-50 sm:pr-3 ${aberto ? 'bg-slate-50/60' : ''}`}>
        <button
          type="button"
          onClick={aoAlternar}
          aria-expanded={aberto}
          aria-label={rotulo}
          className="flex min-w-0 flex-1 items-center gap-3 px-4 py-3.5 text-left sm:px-5"
        >
          <Seta size={18} className="shrink-0 text-slate-400" aria-hidden="true" />

          <span className="min-w-0 flex-1">
            <span className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">{titulo}</span>
            {linhas.filter(Boolean).map((linha, indice) => (
              <span key={indice} className="mt-0.5 block truncate text-xs text-slate-500">
                {linha}
              </span>
            ))}
          </span>

          {lateral}
        </button>

        {atalhos && <span className="flex shrink-0 items-center gap-1">{atalhos}</span>}
      </div>

      {aberto && <div className="space-y-5 border-t border-slate-100 px-4 pb-5 pt-4 sm:px-5 sm:pl-12">{children}</div>}
    </li>
  );
}

/**
 * Tópico do registro aberto: o título em destaque, com ícone, e o conteúdo numa caixa logo
 * abaixo. Sem caixa, o conteúdo (chips, listas) fica direto sob o título.
 */
export function Topico({
  titulo,
  icone,
  semCaixa = false,
  className = '',
  children,
}: {
  titulo: ReactNode;
  icone?: ReactNode;
  semCaixa?: boolean;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section className={`min-w-0 ${className}`}>
      <h4 className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-900">
        {icone && (
          <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-brand-50 text-brand" aria-hidden="true">
            {icone}
          </span>
        )}
        {titulo}
      </h4>

      {semCaixa ? (
        children
      ) : (
        <div className="whitespace-pre-line rounded-xl border border-slate-200/80 bg-slate-50 px-4 py-3 text-sm leading-relaxed text-slate-700">
          {children}
        </div>
      )}
    </section>
  );
}

/** Dado curto do registro (duração, validade, quem assina): rótulo em destaque e valor. */
export function Dado({ rotulo, children }: { rotulo: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border border-slate-200/80 bg-slate-50 px-4 py-2.5">
      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{rotulo}</dt>
      <dd className="mt-0.5 text-sm font-medium text-slate-900">{children}</dd>
    </div>
  );
}

/** Ações do registro aberto, separadas do conteúdo por uma linha. */
export function BarraDeAcoes({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap gap-2 border-t border-slate-100 pt-4">{children}</div>;
}

/** Atalho em forma de etiqueta clicável (origem, tratamento, documentos). */
export function Atalho({
  icone,
  children,
  aoClicar,
  titulo,
}: {
  icone?: ReactNode;
  children: ReactNode;
  aoClicar?: () => void;
  titulo?: string;
}) {
  const classes =
    'inline-flex max-w-full items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700';

  if (!aoClicar) {
    return (
      <span className={classes} title={titulo}>
        {icone}
        <span className="min-w-0 text-left">{children}</span>
      </span>
    );
  }

  return (
    <button
      type="button"
      onClick={aoClicar}
      title={titulo}
      className={`${classes} transition hover:border-brand/40 hover:bg-brand-50 hover:text-brand-dark`}
    >
      {icone}
      <span className="min-w-0 text-left">{children}</span>
    </button>
  );
}
