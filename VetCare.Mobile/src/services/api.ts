import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';

export const CHAVE_TOKEN = '@VetCareMobile:token';
export const CHAVE_USUARIO = '@VetCareMobile:usuario';

/** Porta em que a API escuta em desenvolvimento; muda com EXPO_PUBLIC_API_PORT. */
const PORTA_DA_API = process.env.EXPO_PUBLIC_API_PORT ?? '5265';

/**
 * O celular não enxerga o `localhost` do computador que roda a API. Em
 * desenvolvimento, descobrimos o IP da máquina a partir do host do próprio
 * Metro/Expo, o que evita ter que editar este arquivo a cada troca de rede.
 * Para apontar para outro servidor (inclusive em HTTPS), defina `EXPO_PUBLIC_API_URL`.
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
    return `http://${ip}:${PORTA_DA_API}`;
  }

  // Última alternativa: emulador Android acessa o host pelo endereço 10.0.2.2.
  return `http://10.0.2.2:${PORTA_DA_API}`;
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

/** Telas que funcionam sem sessão: um 401 nelas é resposta de negócio, não sessão expirada. */
const ROTAS_PUBLICAS = ['/usuarios/login', '/usuarios/recuperar-senha', '/usuarios/redefinir-senha'];

let aoPerderSessao: (() => void) | null = null;

/**
 * O AuthContext registra aqui o que fazer quando a API recusa a sessão (token expirado
 * ou conta desativada): voltar ao login. Sem isso o tutor ficaria preso vendo "não foi
 * possível carregar" em todas as abas.
 */
export function registrarPerdaDeSessao(acao: (() => void) | null) {
  aoPerderSessao = acao;
}

api.interceptors.response.use(
  (resposta) => resposta,
  async (erro) => {
    const naoAutenticado = axios.isAxiosError(erro) && erro.response?.status === 401;
    const url = erro?.config?.url ?? '';
    const emRotaPublica = ROTAS_PUBLICAS.some((rota) => url.includes(rota));

    if (naoAutenticado && !emRotaPublica) {
      await AsyncStorage.multiRemove([CHAVE_TOKEN, CHAVE_USUARIO]);
      aoPerderSessao?.();
    }

    return Promise.reject(erro);
  },
);

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

  if (erro instanceof Error && erro.message) {
    return erro.message;
  }

  return alternativa;
}

/** Verdadeiro quando a API respondeu recusando a sessão (token inválido, expirado ou conta inativa). */
export function sessaoRecusada(erro: unknown): boolean {
  return axios.isAxiosError(erro) && (erro.response?.status === 401 || erro.response?.status === 403);
}

/** Monta a URL absoluta de um arquivo servido pela API (mídias e documentos, já com assinatura). */
export function urlDoArquivo(caminhoRelativo: string): string {
  if (!caminhoRelativo) {
    return '';
  }

  return caminhoRelativo.startsWith('http') ? caminhoRelativo : `${URL_API}${caminhoRelativo}`;
}
