import { Text, type TextStyle } from 'react-native';

/**
 * Ícones em texto. O projeto não inclui uma biblioteca de ícones vetoriais, e
 * usar símbolos Unicode evita adicionar dependência nativa — que exigiria build
 * customizado e sairia do fluxo do Expo Go usado no desenvolvimento.
 */
const SIMBOLOS: Record<string, string> = {
  pet: '🐾',
  cachorro: '🐕',
  gato: '🐈',
  agenda: '📅',
  relogio: '🕒',
  mensagem: '💬',
  sino: '🔔',
  perfil: '👤',
  sair: '⎋',
  seta: '›',
  voltar: '‹',
  ok: '✓',
  cancelar: '✕',
  info: 'ℹ',
  alerta: '⚠',
  documento: '📄',
  grafico: '📈',
  estetoscopio: '🩺',
  peso: '⚖',
  enviar: '➤',
  atualizar: '↻',
};

interface Props {
  nome: keyof typeof SIMBOLOS | string;
  tamanho?: number;
  cor?: string;
  estilo?: TextStyle;
}

export function Icone({ nome, tamanho = 18, cor, estilo }: Props) {
  return (
    <Text style={[{ fontSize: tamanho, color: cor, lineHeight: tamanho * 1.25 }, estilo]}>
      {SIMBOLOS[nome] ?? '•'}
    </Text>
  );
}

/** Ícone da espécie do paciente, com alternativa genérica. */
export function iconeDaEspecie(especie: string): string {
  const normalizada = especie.trim().toLowerCase();

  if (normalizada.includes('cach') || normalizada.includes('cão') || normalizada.includes('cao')) return 'cachorro';
  if (normalizada.includes('gat')) return 'gato';

  return 'pet';
}
