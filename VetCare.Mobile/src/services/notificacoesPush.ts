import { Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { api } from './api';

/**
 * HU-015: notificações no celular do tutor. O aparelho pede permissão, obtém o token
 * de push da Expo e o registra na API; a partir daí cada aviso gravado no sistema
 * chega também como notificação, mesmo com o aplicativo fechado.
 *
 * Limitações conhecidas, tratadas aqui sem quebrar o aplicativo:
 * - em emulador não há serviço de push;
 * - no Expo Go para Android (SDK 53 em diante) o push remoto não é suportado — é
 *   preciso um build de desenvolvimento (`npx expo run:android`) ou um build EAS;
 * - o token exige o `projectId` do EAS em `extra.eas.projectId` no app.json (`npx eas init`).
 */

export type EstadoDoPush = 'ativo' | 'sem-permissao' | 'indisponivel' | 'nao-configurado' | 'erro' | 'desconhecido';

export interface SituacaoDoPush {
  estado: EstadoDoPush;
  motivo?: string;
}

const CHAVE_TOKEN_PUSH = '@VetCareMobile:tokenPush';

/** Canal padrão do Android; precisa existir antes do pedido de permissão. */
const CANAL_PADRAO = 'default';

// Com o aplicativo aberto, a notificação é mostrada mesmo assim: a tela que o tutor
// está vendo pode não ser a do aviso.
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

function obterProjectId(): string | undefined {
  const doExpo = (Constants.expoConfig?.extra as { eas?: { projectId?: string } } | undefined)?.eas?.projectId;
  return doExpo ?? Constants.easConfig?.projectId ?? undefined;
}

/**
 * Registra este aparelho para o usuário autenticado. Nunca lança: todo caminho sem
 * push devolve um estado explicativo, exibido na tela de perfil.
 */
export async function registrarAparelhoParaNotificacoes(): Promise<SituacaoDoPush> {
  if (!Device.isDevice) {
    return { estado: 'indisponivel', motivo: 'Emuladores não recebem notificações. Use um celular de verdade.' };
  }

  try {
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync(CANAL_PADRAO, {
        name: 'Avisos do VetCare',
        importance: Notifications.AndroidImportance.MAX,
        vibrationPattern: [0, 250, 250, 250],
        lightColor: '#0284c7',
      });
    }

    let { status } = await Notifications.getPermissionsAsync();

    if (status !== 'granted') {
      ({ status } = await Notifications.requestPermissionsAsync());
    }

    if (status !== 'granted') {
      return { estado: 'sem-permissao', motivo: 'Permita as notificações do VetCare nas configurações do celular.' };
    }

    const projectId = obterProjectId();

    if (!projectId) {
      return {
        estado: 'nao-configurado',
        motivo: 'O aplicativo ainda não tem o identificador do projeto Expo (extra.eas.projectId no app.json).',
      };
    }

    const { data: tokenPush } = await Notifications.getExpoPushTokenAsync({ projectId });

    await api.post('/api/dispositivos', {
      tokenPush,
      plataforma: Platform.OS,
      nomeDoAparelho: [Device.manufacturer, Device.modelName].filter(Boolean).join(' '),
    });

    await AsyncStorage.setItem(CHAVE_TOKEN_PUSH, tokenPush);

    return { estado: 'ativo' };
  } catch (falha) {
    const mensagem = falha instanceof Error ? falha.message : String(falha);

    // Expo Go no Android avisa exatamente isso: só um build de desenvolvimento recebe push.
    const semSuporte = /Expo Go/i.test(mensagem);

    return {
      estado: semSuporte ? 'indisponivel' : 'erro',
      motivo: semSuporte
        ? 'O Expo Go não recebe notificações no Android. Instale o aplicativo com "npx expo run:android" ou um build EAS.'
        : mensagem,
    };
  }
}

/** Ao sair da conta, o aparelho deixa de receber os avisos deste usuário. */
export async function removerAparelhoDasNotificacoes(): Promise<void> {
  const tokenPush = await AsyncStorage.getItem(CHAVE_TOKEN_PUSH);

  if (!tokenPush) {
    return;
  }

  try {
    await api.delete('/api/dispositivos', { params: { token: tokenPush } });
  } catch {
    // Sem rede a API não fica sabendo agora; o próximo login de outra pessoa neste
    // aparelho troca o dono do token de qualquer forma.
  }

  await AsyncStorage.removeItem(CHAVE_TOKEN_PUSH);
}

/** Dados que a API envia junto com cada push (ver EntregadorDeNotificacoes). */
export interface DadosDaNotificacao {
  notificacaoId?: string;
  tipo?: string;
  link?: string;
}

export function dadosDaResposta(resposta: Notifications.NotificationResponse | null): DadosDaNotificacao {
  return (resposta?.notification.request.content.data ?? {}) as DadosDaNotificacao;
}
