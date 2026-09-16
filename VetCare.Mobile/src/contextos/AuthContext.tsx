import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { api, CHAVE_TOKEN, CHAVE_USUARIO } from '../services/api';
import type { RespostaLogin, Usuario } from '../tipos';

interface DadosAuth {
  usuario: Usuario | null;
  carregando: boolean;
  entrar: (email: string, senha: string) => Promise<void>;
  sair: () => Promise<void>;
}

const AuthContext = createContext<DadosAuth>({} as DadosAuth);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null);
  const [carregando, setCarregando] = useState(true);

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
          await AsyncStorage.setItem(CHAVE_USUARIO, JSON.stringify(data));
          setUsuario(data);
        }
      } catch {
        // Sessão inválida: limpamos para que o app volte à tela de login.
        await AsyncStorage.multiRemove([CHAVE_TOKEN, CHAVE_USUARIO]);

        if (ativo) {
          setUsuario(null);
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

  const entrar = useCallback(async (email: string, senha: string) => {
    const { data } = await api.post<RespostaLogin>('/api/usuarios/login', { email, senha });

    await AsyncStorage.setItem(CHAVE_TOKEN, data.token);
    await AsyncStorage.setItem(CHAVE_USUARIO, JSON.stringify(data.usuario));

    setUsuario(data.usuario);
  }, []);

  const sair = useCallback(async () => {
    await AsyncStorage.multiRemove([CHAVE_TOKEN, CHAVE_USUARIO]);
    setUsuario(null);
  }, []);

  const valor = useMemo(() => ({ usuario, carregando, entrar, sair }), [usuario, carregando, entrar, sair]);

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
