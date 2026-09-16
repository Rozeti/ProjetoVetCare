import axios from 'axios';

export const URL_API = import.meta.env.VITE_API_URL ?? 'http://localhost:5265';

export const CHAVE_TOKEN = '@VetCare:token';
export const CHAVE_USUARIO = '@VetCare:usuario';

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
    const jaEstaNoLogin = window.location.pathname === '/login';

    if (naoAutenticado && !ehChamadaDeLogin && !jaEstaNoLogin) {
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

/** Monta a URL absoluta de um arquivo servido pela API (mídias e documentos). */
export function urlDoArquivo(caminhoRelativo: string): string {
  if (!caminhoRelativo) {
    return '';
  }

  return caminhoRelativo.startsWith('http') ? caminhoRelativo : `${URL_API}${caminhoRelativo}`;
}
