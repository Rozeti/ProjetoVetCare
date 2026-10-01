import { createNavigationContainerRef } from '@react-navigation/native';

/** Telas da pilha principal e da barra de abas, para a navegação ser verificada pelo compilador. */
export type RotasDaPilha = {
  Login: { mensagem?: string } | undefined;
  RecuperarSenha: undefined;
  Principal: { screen?: keyof RotasDasAbas } | undefined;
  Prontuario: { pacienteId: string; nome?: string };
};

export type RotasDasAbas = {
  MeusPets: undefined;
  MinhaAgenda: undefined;
  Mensagens: undefined;
  Notificacoes: undefined;
  Perfil: undefined;
};

/**
 * Referência global do navegador. É o que permite abrir a tela certa quando o tutor
 * toca numa notificação, mesmo fora da árvore de componentes.
 */
export const navegacaoRef = createNavigationContainerRef<RotasDaPilha>();

/**
 * Traduz o caminho do portal web que vem na notificação ("/minha-agenda",
 * "/prontuario/{id}") para a tela equivalente do aplicativo. Devolve falso quando o
 * navegador ainda não está pronto, para o chamador tentar de novo.
 */
export function abrirDestinoDaNotificacao(link: string | null | undefined): boolean {
  if (!navegacaoRef.isReady()) {
    return false;
  }

  const prontuario = link?.match(/^\/prontuario\/([0-9a-f-]{36})/i);

  if (prontuario) {
    navegacaoRef.navigate('Prontuario', { pacienteId: prontuario[1] });
    return true;
  }

  const aba = abaDoLink(link);
  navegacaoRef.navigate('Principal', { screen: aba });

  return true;
}

function abaDoLink(link: string | null | undefined): keyof RotasDasAbas {
  if (!link) return 'Notificacoes';
  if (link.startsWith('/minha-agenda') || link.startsWith('/agenda')) return 'MinhaAgenda';
  if (link.startsWith('/mensagens')) return 'Mensagens';
  if (link.startsWith('/meus-pets')) return 'MeusPets';

  return 'Notificacoes';
}
