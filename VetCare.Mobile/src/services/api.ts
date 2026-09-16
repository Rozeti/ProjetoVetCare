import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';

export const CHAVE_TOKEN = '@VetCareMobile:token';
export const CHAVE_USUARIO = '@VetCareMobile:usuario';

/**
 * O celular não enxerga o `localhost` do computador que roda a API. Em
 * desenvolvimento, descobrimos o IP da máquina a partir do host do próprio
 * Metro/Expo, o que evita ter que editar este arquivo a cada troca de rede.
 * Para apontar para outro servidor, defina `EXPO_PUBLIC_API_URL`.
 */
function descobrirUrlDaApi(): string {
  const configurada = process.env.EXPO_PUBLIC_API_URL;

  if (configurada) {
    return configurada;
  }

  const hostDoExpo =
    Constants.expoConfig?.hostUri ??
    (Constants.expoGoConfig as { debuggerHost?: string } | undefined)?.debuggerHost;

  if (hostDoExpo) {
    const ip = hostDoExpo.split(':')[0];
    return `http://${ip}:5265`;
  }

  // Última alternativa: emulador Android acessa o host pelo endereço 10.0.2.2.
  return 'http://10.0.2.2:5265';
}

export const URL_API = descobrirUrlDaApi();

export const api = axios.create({
  baseURL: URL_API,
  timeout: 15000,
});

api.interceptors.request.use(async (config) => {
  const token = await AsyncStorage.getItem(CHAVE_TOKEN);

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

/** Extrai a mensagem que a API envia em `{ mensagem }`. */
export function mensagemDeErro(erro: unknown, alternativa = 'Não foi possível concluir a operação.'): string {
  if (axios.isAxiosError(erro)) {
    const mensagem = (erro.response?.data as { mensagem?: string } | undefined)?.mensagem;

    if (mensagem) {
      return mensagem;
    }

    if (!erro.response) {
      return `Não foi possível falar com o servidor (${URL_API}). Confira se a API está em execução e se o celular está na mesma rede.`;
    }
  }

  return alternativa;
}

export function urlDoArquivo(caminhoRelativo: string): string {
  if (!caminhoRelativo) {
    return '';
  }

  return caminhoRelativo.startsWith('http') ? caminhoRelativo : `${URL_API}${caminhoRelativo}`;
}
