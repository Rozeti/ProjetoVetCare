import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api, CHAVE_TOKEN, CHAVE_USUARIO } from '../services/api';
import type { Perfil, RespostaLogin, Usuario } from '../types';
import { AuthContext, type DadosAuth } from './auth';

function lerUsuarioSalvo(): Usuario | null {
  const usuarioSalvo = localStorage.getItem(CHAVE_USUARIO);
  const token = localStorage.getItem(CHAVE_TOKEN);

  if (!usuarioSalvo || !token) {
    return null;
  }

  try {
    return JSON.parse(usuarioSalvo) as Usuario;
  } catch {
    // Dado corrompido no armazenamento local não pode impedir o app de abrir.
    localStorage.removeItem(CHAVE_USUARIO);
    localStorage.removeItem(CHAVE_TOKEN);
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(lerUsuarioSalvo);
  const [carregando, setCarregando] = useState(true);

  // Revalida a sessão restaurada do localStorage: o token pode ter expirado
  // enquanto a aba estava fechada.
  useEffect(() => {
    let ativo = true;

    async function revalidar() {
      if (!localStorage.getItem(CHAVE_TOKEN)) {
        if (ativo) setCarregando(false);
        return;
      }

      try {
        const { data } = await api.get<Usuario>('/api/usuarios/me');

        if (ativo) {
          localStorage.setItem(CHAVE_USUARIO, JSON.stringify(data));
          setUsuario(data);
        }
      } catch {
        if (ativo) {
          localStorage.removeItem(CHAVE_TOKEN);
          localStorage.removeItem(CHAVE_USUARIO);
          setUsuario(null);
        }
      } finally {
        if (ativo) setCarregando(false);
      }
    }

    revalidar();

    return () => {
      ativo = false;
    };
  }, []);

  const entrar = useCallback(async (email: string, senha: string) => {
    const { data } = await api.post<RespostaLogin>('/api/usuarios/login', { email, senha });

    localStorage.setItem(CHAVE_TOKEN, data.token);
    localStorage.setItem(CHAVE_USUARIO, JSON.stringify(data.usuario));

    setUsuario(data.usuario);
  }, []);

  // HU-001, CA-4: encerra a sessão e devolve o usuário à tela inicial de login.
  const sair = useCallback(() => {
    localStorage.removeItem(CHAVE_TOKEN);
    localStorage.removeItem(CHAVE_USUARIO);
    setUsuario(null);
  }, []);

  const valor = useMemo<DadosAuth>(() => {
    const temPerfil = (...perfis: Perfil[]) => !!usuario && perfis.includes(usuario.perfil);

    return {
      usuario,
      carregando,
      entrar,
      sair,
      temPerfil,
      ehTutor: usuario?.perfil === 'Tutor',
      ehVeterinario: usuario?.perfil === 'Veterinario',
      ehAdministrador: usuario?.perfil === 'Administrador',
      podeVerObservacoesInternas: temPerfil('Administrador', 'Veterinario'),
    };
  }, [usuario, carregando, entrar, sair]);

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}
