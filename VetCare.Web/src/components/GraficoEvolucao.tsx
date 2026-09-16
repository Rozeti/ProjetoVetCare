import { useMemo } from 'react';
import type { PontoEvolucao } from '../types';
import { formatarData } from '../utils/formato';

interface Props {
  titulo: string;
  pontos: PontoEvolucao[];
  unidade?: string;
  cor?: string;
  /** Fixa a escala vertical, usado pela escala de dor que sempre vai de 0 a 10. */
  minimoFixo?: number;
  maximoFixo?: number;
}

const LARGURA = 560;
const ALTURA = 200;
const MARGEM = { topo: 16, direita: 16, base: 32, esquerda: 44 };

/**
 * HU-011, CA-3: gráfico de evolução do prontuário (peso e escala de dor).
 * Desenhado em SVG puro — a série é curta e não justifica uma biblioteca de
 * gráficos, que pesaria no carregamento exigido pelo RNF-004.
 */
export function GraficoEvolucao({
  titulo,
  pontos,
  unidade = '',
  cor = '#0284c7',
  minimoFixo,
  maximoFixo,
}: Props) {
  const desenho = useMemo(() => {
    if (pontos.length === 0) {
      return null;
    }

    const valores = pontos.map((p) => p.valor);
    const minimoBruto = minimoFixo ?? Math.min(...valores);
    const maximoBruto = maximoFixo ?? Math.max(...valores);

    // Com um único ponto, ou série constante, damos uma folga para a linha não
    // ficar colada na borda do gráfico.
    const folga = maximoBruto === minimoBruto ? Math.max(1, maximoBruto * 0.1) : (maximoBruto - minimoBruto) * 0.15;
    const minimo = minimoFixo ?? minimoBruto - folga;
    const maximo = maximoFixo ?? maximoBruto + folga;

    const larguraUtil = LARGURA - MARGEM.esquerda - MARGEM.direita;
    const alturaUtil = ALTURA - MARGEM.topo - MARGEM.base;

    const x = (indice: number) =>
      pontos.length === 1
        ? MARGEM.esquerda + larguraUtil / 2
        : MARGEM.esquerda + (indice / (pontos.length - 1)) * larguraUtil;

    const y = (valor: number) =>
      MARGEM.topo + alturaUtil - ((valor - minimo) / (maximo - minimo || 1)) * alturaUtil;

    const coordenadas = pontos.map((ponto, indice) => ({
      ...ponto,
      cx: x(indice),
      cy: y(ponto.valor),
    }));

    const linha = coordenadas.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.cx} ${c.cy}`).join(' ');

    const area =
      coordenadas.length > 1
        ? `${linha} L ${coordenadas[coordenadas.length - 1].cx} ${ALTURA - MARGEM.base} L ${coordenadas[0].cx} ${ALTURA - MARGEM.base} Z`
        : '';

    const marcasY = [0, 0.5, 1].map((fracao) => {
      const valor = minimo + (maximo - minimo) * (1 - fracao);
      return { valor, y: MARGEM.topo + alturaUtil * fracao };
    });

    return { coordenadas, linha, area, marcasY };
  }, [pontos, minimoFixo, maximoFixo]);

  const formatarValor = (valor: number) =>
    `${valor.toFixed(1).replace('.', ',')}${unidade ? ` ${unidade}` : ''}`;

  return (
    <div>
      <div className="mb-3 flex items-baseline justify-between gap-3">
        <h3 className="text-sm font-semibold text-slate-700">{titulo}</h3>
        {pontos.length > 0 && (
          <span className="text-xs text-slate-500">
            Atual: <strong className="text-slate-700">{formatarValor(pontos[pontos.length - 1].valor)}</strong>
          </span>
        )}
      </div>

      {!desenho ? (
        <p className="rounded-xl bg-slate-50 px-4 py-8 text-center text-sm text-slate-500">
          Ainda não há registros suficientes para montar este gráfico.
        </p>
      ) : (
        <svg
          viewBox={`0 0 ${LARGURA} ${ALTURA}`}
          className="w-full"
          role="img"
          aria-label={`${titulo}. ${pontos
            .map((p) => `${formatarData(p.data)}: ${formatarValor(p.valor)}`)
            .join('. ')}`}
        >
          <defs>
            <linearGradient id={`area-${titulo.replace(/\s/g, '')}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={cor} stopOpacity="0.22" />
              <stop offset="100%" stopColor={cor} stopOpacity="0" />
            </linearGradient>
          </defs>

          {desenho.marcasY.map((marca) => (
            <g key={marca.y}>
              <line
                x1={MARGEM.esquerda}
                y1={marca.y}
                x2={LARGURA - MARGEM.direita}
                y2={marca.y}
                stroke="#e2e8f0"
                strokeWidth="1"
              />
              <text x={MARGEM.esquerda - 8} y={marca.y + 4} textAnchor="end" fontSize="10" fill="#94a3b8">
                {marca.valor.toFixed(1).replace('.', ',')}
              </text>
            </g>
          ))}

          {desenho.area && <path d={desenho.area} fill={`url(#area-${titulo.replace(/\s/g, '')})`} />}

          <path d={desenho.linha} fill="none" stroke={cor} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />

          {desenho.coordenadas.map((ponto, indice) => (
            <g key={`${ponto.data}-${indice}`}>
              <circle cx={ponto.cx} cy={ponto.cy} r="4.5" fill="white" stroke={cor} strokeWidth="2.5" />
              <title>{`${formatarData(ponto.data)} — ${formatarValor(ponto.valor)}`}</title>
            </g>
          ))}

          {/* Com muitos pontos só rotulamos o primeiro e o último, para não poluir o eixo. */}
          {desenho.coordenadas.map((ponto, indice) => {
            const rotular =
              desenho.coordenadas.length <= 5 || indice === 0 || indice === desenho.coordenadas.length - 1;

            if (!rotular) return null;

            return (
              <text
                key={`rotulo-${indice}`}
                x={ponto.cx}
                y={ALTURA - MARGEM.base + 18}
                textAnchor="middle"
                fontSize="10"
                fill="#94a3b8"
              >
                {formatarData(ponto.data).slice(0, 5)}
              </text>
            );
          })}
        </svg>
      )}
    </div>
  );
}
