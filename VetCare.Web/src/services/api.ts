import axios from 'axios';

export const URL_API = import.meta.env.VITE_API_URL ?? 'http://localhost:5265';

export const CHAVE_TOKEN = '@VetCare:token';
export const CHAVE_USUARIO = '@VetCare:usuario';

/** Telas que funcionam sem sessão: um 401 nelas é resposta de negócio, não sessão expirada. */
const TELAS_PUBLICAS = ['/login', '/recuperar-senha', '/primeiro-acesso'];

export const api = axios.create({
  baseURL: URL_API,
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(CHAVE_TOKEN);

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

api.interceptors.response.use(
  (resposta) => resposta,
  (erro) => {
    // RNF-002: sessão expirada ou token inválido devolve o usuário para o login.
    // A própria chamada de login também responde 401 em credenciais inválidas —
    // nesse caso a tela precisa mostrar a mensagem, não redirecionar.
    const naoAutenticado = erro?.response?.status === 401;
    const ehChamadaDeLogin = (erro?.config?.url ?? '').includes('/usuarios/login');
    const emTelaPublica = TELAS_PUBLICAS.includes(window.location.pathname);

    if (naoAutenticado && !ehChamadaDeLogin && !emTelaPublica) {
      localStorage.removeItem(CHAVE_TOKEN);
      localStorage.removeItem(CHAVE_USUARIO);
      window.location.href = '/login';
    }

    return Promise.reject(erro);
  },
);

/**
 * Extrai a mensagem de erro que a API envia no corpo `{ mensagem }`, com uma
 * alternativa legível quando a falha é de rede.
 */
export function mensagemDeErro(erro: unknown, alternativa = 'Não foi possível concluir a operação.'): string {
  if (axios.isAxiosError(erro)) {
    const mensagem = (erro.response?.data as { mensagem?: string } | undefined)?.mensagem;

    if (mensagem) {
      return mensagem;
    }

    if (!erro.response) {
      return 'Não foi possível falar com o servidor. Verifique se a API está em execução.';
    }
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
