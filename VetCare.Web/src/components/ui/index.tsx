import { useEffect, type ReactNode } from 'react';
import { AlertCircle, CheckCircle2, Info, Loader2, X } from 'lucide-react';

/** Peças visuais reutilizadas em todas as telas, conforme o design system (RNF-008). */

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <div className={`vc-card ${className}`}>{children}</div>;
}

export function CabecalhoPagina({
  titulo,
  descricao,
  acoes,
}: {
  titulo: string;
  descricao?: string;
  acoes?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-4 mb-6">
      <div>
        <h1 className="vc-titulo-pagina">{titulo}</h1>
        {descricao && <p className="vc-subtitulo">{descricao}</p>}
      </div>
      {acoes && <div className="flex flex-wrap items-center gap-2">{acoes}</div>}
    </div>
  );
}

export function Etiqueta({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <span className={`vc-etiqueta ${className}`}>{children}</span>;
}

export function Carregando({ texto = 'Carregando...' }: { texto?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-12 text-slate-500" role="status">
      <Loader2 className="animate-spin" size={20} />
      <span className="text-sm">{texto}</span>
    </div>
  );
}

/**
 * Estado vazio explicativo. Vários critérios de aceite pedem que a ausência de
 * dados apareça como informação, e não como tela de erro (HU-005 CA-3, HU-016 CA-3).
 */
export function SemDados({
  icone,
  titulo,
  descricao,
  acao,
}: {
  icone?: ReactNode;
  titulo: string;
  descricao?: string;
  acao?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center py-14 px-6 text-center">
      <div className="mb-3 text-slate-300">{icone ?? <Info size={40} />}</div>
      <p className="font-semibold text-slate-700">{titulo}</p>
      {descricao && <p className="mt-1 text-sm text-slate-500 max-w-md">{descricao}</p>}
      {acao && <div className="mt-4">{acao}</div>}
    </div>
  );
}

export function Alerta({
  tipo = 'erro',
  children,
  aoFechar,
}: {
  tipo?: 'erro' | 'sucesso' | 'aviso' | 'info';
  children: ReactNode;
  aoFechar?: () => void;
}) {
  const estilos = {
    erro: { classe: 'bg-perigo-claro text-red-800 border-red-200', Icone: AlertCircle },
    sucesso: { classe: 'bg-sucesso-claro text-emerald-800 border-emerald-200', Icone: CheckCircle2 },
    aviso: { classe: 'bg-alerta-claro text-amber-800 border-amber-200', Icone: AlertCircle },
    info: { classe: 'bg-brand-100 text-brand-dark border-brand-200', Icone: Info },
  }[tipo];

  const { Icone } = estilos;

  return (
    <div
      className={`flex items-start gap-2.5 rounded-xl border px-4 py-3 text-sm ${estilos.classe}`}
      role={tipo === 'erro' ? 'alert' : 'status'}
    >
      <Icone size={18} className="mt-0.5 shrink-0" />
      <div className="flex-1">{children}</div>
      {aoFechar && (
        <button type="button" onClick={aoFechar} className="shrink-0 opacity-60 hover:opacity-100" aria-label="Fechar">
          <X size={16} />
        </button>
      )}
    </div>
  );
}

export function Modal({
  aberto,
  titulo,
  descricao,
  children,
  aoFechar,
  largura = 'max-w-lg',
}: {
  aberto: boolean;
  titulo: string;
  descricao?: string;
  children: ReactNode;
  aoFechar: () => void;
  largura?: string;
}) {
  // Fechar com Esc é o atalho que o usuário espera de um diálogo.
  useEffect(() => {
    if (!aberto) return;

    function aoTeclar(evento: KeyboardEvent) {
      if (evento.key === 'Escape') aoFechar();
    }

    document.addEventListener('keydown', aoTeclar);
    document.body.style.overflow = 'hidden';

    return () => {
      document.removeEventListener('keydown', aoTeclar);
      document.body.style.overflow = '';
    };
  }, [aberto, aoFechar]);

  if (!aberto) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/50 p-4 sm:p-8">
      <div
        className={`w-full ${largura} vc-card my-auto`}
        role="dialog"
        aria-modal="true"
        aria-label={titulo}
      >
        <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-6 py-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900">{titulo}</h2>
            {descricao && <p className="mt-0.5 text-sm text-slate-500">{descricao}</p>}
          </div>
          <button
            type="button"
            onClick={aoFechar}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
            aria-label="Fechar"
          >
            <X size={20} />
          </button>
        </div>
        <div className="px-6 py-5">{children}</div>
      </div>
    </div>
  );
}

export function Campo({
  rotulo,
  obrigatorio,
  dica,
  erro,
  children,
}: {
  rotulo: string;
  obrigatorio?: boolean;
  dica?: string;
  erro?: string;
  children: ReactNode;
}) {
  return (
    <div>
      <label className="vc-rotulo">
        {rotulo}
        {obrigatorio && <span className="ml-0.5 text-perigo">*</span>}
      </label>
      {children}
      {dica && !erro && <p className="mt-1 text-xs text-slate-500">{dica}</p>}
      {erro && <p className="mt-1 text-xs text-perigo">{erro}</p>}
    </div>
  );
}

export function Avatar({ nome, cor }: { nome: string; cor?: string }) {
  const letras = nome
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase();

  return (
    <div
      className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white"
      style={{ backgroundColor: cor ?? '#0284c7' }}
      aria-hidden="true"
    >
      {letras || '?'}
    </div>
  );
}

export function Estatistica({
  rotulo,
  valor,
  icone,
  cor = 'text-brand',
  fundo = 'bg-brand-100',
  destaque,
}: {
  rotulo: string;
  valor: number | string;
  icone: ReactNode;
  cor?: string;
  fundo?: string;
  destaque?: string;
}) {
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-medium text-slate-500">{rotulo}</p>
          <p className="mt-1 text-3xl font-bold text-slate-900">{valor}</p>
          {destaque && <p className="mt-1 text-xs text-slate-500">{destaque}</p>}
        </div>
        <div className={`rounded-xl p-2.5 ${fundo} ${cor}`}>{icone}</div>
      </div>
    </Card>
  );
}
