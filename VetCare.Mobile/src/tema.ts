/**
 * RNF-008 — Design system compartilhado com o VetCare Web: mesma paleta,
 * mesmos raios de canto e a mesma hierarquia tipográfica, para que o tutor
 * reconheça o produto nas duas plataformas.
 */
export const cores = {
  fundo: '#f8fafc',
  superficie: '#ffffff',
  borda: '#e2e8f0',

  marca: '#0284c7',
  marcaClara: '#e0f2fe',
  marcaEscura: '#0369a1',

  texto: '#0f172a',
  textoSecundario: '#64748b',
  textoSuave: '#94a3b8',

  sucesso: '#059669',
  sucessoClaro: '#d1fae5',
  alerta: '#d97706',
  alertaClaro: '#fef3c7',
  perigo: '#dc2626',
  perigoClaro: '#fee2e2',
  info: '#7c3aed',
  infoClaro: '#ede9fe',
} as const;

export const espacos = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
} as const;

export const raios = {
  sm: 8,
  md: 12,
  lg: 16,
  cheio: 999,
} as const;

/** Sombra sutil dos cards, equivalente nas duas plataformas. */
export const sombraCard = {
  elevation: 2,
  shadowColor: '#0f172a',
  shadowOffset: { width: 0, height: 2 },
  shadowOpacity: 0.06,
  shadowRadius: 6,
} as const;

/** HU-004, CA-4: cada status da sessão recebe um badge com cores próprias. */
export const estiloStatus: Record<string, { fundo: string; texto: string }> = {
  'Aguardando confirmação': { fundo: cores.alertaClaro, texto: '#92400e' },
  Confirmada: { fundo: cores.sucessoClaro, texto: '#065f46' },
  Cancelada: { fundo: cores.perigoClaro, texto: '#991b1b' },
  'Concluída': { fundo: cores.marcaClara, texto: cores.marcaEscura },
};

/** Escala de dor 0–10, com a mesma leitura visual do sistema web. */
export function estiloEscalaDor(valor: number) {
  if (valor <= 3) return { fundo: cores.sucessoClaro, texto: '#065f46' };
  if (valor <= 6) return { fundo: cores.alertaClaro, texto: '#92400e' };
  return { fundo: cores.perigoClaro, texto: '#991b1b' };
}
