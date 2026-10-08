import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { api, CHAVE_TOKEN, CHAVE_USUARIO, registrarPerdaDeSessao, sessaoRecusada } from '../services/api';
import {
  registrarAparelhoParaNotificacoes,
  removerAparelhoDasNotificacoes,
  type SituacaoDoPush,
} from '../services/notificacoesPush';
import type { RespostaLogin, Usuario } from '../tipos';

interface DadosAuth {
  usuario: Usuario | null;
  carregando: boolean;
  entrar: (email: string, senha: string) => Promise<void>;
  sair: () => Promise<void>;
  /** Guarda a versão mais recente dos dados do próprio usuário (preferências, contato). */
  atualizarUsuario: (usuario: Usuario) => void;
  /** Situação das notificações neste aparelho, exibida na tela de perfil. */
  push: SituacaoDoPush;
  /** Tenta registrar o aparelho de novo (por exemplo, depois de a permissão ser concedida). */
  reativarPush: () => Promise<void>;
}

const AuthContext = createContext<DadosAuth>({} as DadosAuth);

async function guardarUsuario(usuario: Usuario) {
  await AsyncStorage.setItem(CHAVE_USUARIO, JSON.stringify(usuario));
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [push, setPush] = useState<SituacaoDoPush>({ estado: 'desconhecido' });

  // Restaura a sessão guardada no aparelho e confirma com a API se o token
  // continua válido — ele pode ter expirado com o app fechado.
  useEffect(() => {
    let ativo = true;

    async function restaurar() {
      try {
        const token = await AsyncStorage.getItem(CHAVE_TOKEN);

        if (!token) {
          return;
        }

        const guardado = await AsyncStorage.getItem(CHAVE_USUARIO);

        if (guardado && ativo) {
          setUsuario(JSON.parse(guardado) as Usuario);
        }

        const { data } = await api.get<Usuario>('/api/usuarios/me');

        if (ativo) {
          await guardarUsuario(data);
          setUsuario(data);
        }
      } catch (falha) {
        // Só a recusa da API encerra a sessão. Sem rede, o tutor continua entrando
        // com o que estava guardado; a próxima chamada tenta de novo.
        if (sessaoRecusada(falha)) {
          await AsyncStorage.multiRemove([CHAVE_TOKEN, CHAVE_USUARIO]);

          if (ativo) {
            setUsuario(null);
          }
        }
      } finally {
        if (ativo) {
          setCarregando(false);
        }
      }
    }

    restaurar();

    return () => {
      ativo = false;
    };
  }, []);

  // Sessão recusada pela API no meio do uso (token expirado, conta desativada): o
  // interceptor já apagou o token; aqui o app volta para a tela de login.
  useEffect(() => {
    registrarPerdaDeSessao(() => {
      setUsuario(null);
      setPush({ estado: 'desconhecido' });
    });

    return () => registrarPerdaDeSessao(null);
  }, []);

  // Com usuário na sessão, o aparelho é (re)registrado para receber os avisos: o token
  // de push pode mudar entre instalações, e a API guarda sempre o mais recente.
  useEffect(() => {
    if (!usuario) {
      return;
    }

    let ativo = true;

    registrarAparelhoParaNotificacoes().then((situacao) => {
      if (ativo) setPush(situacao);
    });

    return () => {
      ativo = false;
    };
  }, [usuario?.id]);

  const entrar = useCallback(async (email: string, senha: string) => {
    const { data } = await api.post<RespostaLogin>('/api/usuarios/login', { email, senha });

    // Este aplicativo é a área do tutor; a equipe da clínica trabalha pelo portal web.
    if (data.usuario.perfil !== 'Tutor') {
      throw new Error('Este aplicativo é exclusivo para tutores. A equipe da clínica usa o portal web.');
    }

    await AsyncStorage.setItem(CHAVE_TOKEN, data.token);
    await guardarUsuario(data.usuario);

    setUsuario(data.usuario);
  }, []);

  const sair = useCallback(async () => {
    // Antes de descartar o token: a chamada precisa dele para a API saber de quem é o aparelho.
    await removerAparelhoDasNotificacoes();
    await AsyncStorage.multiRemove([CHAVE_TOKEN, CHAVE_USUARIO]);
    setUsuario(null);
    setPush({ estado: 'desconhecido' });
  }, []);

  const atualizarUsuario = useCallback((dados: Usuario) => {
    guardarUsuario(dados);
    setUsuario(dados);
  }, []);

  const reativarPush = useCallback(async () => {
    setPush(await registrarAparelhoParaNotificacoes());
  }, []);

  const valor = useMemo(
    () => ({ usuario, carregando, entrar, sair, atualizarUsuario, push, reativarPush }),
    [usuario, carregando, entrar, sair, atualizarUsuario, push, reativarPush],
  );

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
