import { createContext, useContext } from 'react';
import type { Perfil, Usuario } from '../types';

export interface DadosAuth {
  usuario: Usuario | null;
  carregando: boolean;
  entrar: (email: string, senha: string) => Promise<void>;
  sair: () => void;
  /** RN-005: base de todas as decisões de navegação e de exibição por perfil. */
  temPerfil: (...perfis: Perfil[]) => boolean;
  ehTutor: boolean;
  ehVeterinario: boolean;
  ehAdministrador: boolean;
  /** RN-003: somente Administrador e Veterinário enxergam observações internas. */
  podeVerObservacoesInternas: boolean;
}

/**
 * O contexto e o hook moram fora do arquivo do provedor para que aquele arquivo
 * exporte apenas componentes, condição para o hot reload do Vite preservar o estado.
 */
export const AuthContext = createContext<DadosAuth>({} as DadosAuth);

export function useAuth() {
  return useContext(AuthContext);
}
