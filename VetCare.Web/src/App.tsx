import type { ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { AuthProvider } from './contexts/AuthContext';
import { AtualizacoesProvider } from './contexts/AtualizacoesContext';
import { useAuth } from './contexts/auth';
import { Layout } from './components/Layout';
import type { Perfil } from './types';

import { Login } from './pages/Login';
import { Dashboard } from './pages/Dashboard';
import { Agenda } from './pages/Agenda';
import { AgendaGeral } from './pages/AgendaGeral';
import { Pacientes } from './pages/Pacientes';
import { Prontuario } from './pages/Prontuario';
import { Tutores } from './pages/Tutores';
import { Usuarios } from './pages/Usuarios';
import { Mensagens } from './pages/Mensagens';
import { Notificacoes } from './pages/Notificacoes';
import { Relatorios } from './pages/Relatorios';
import { Configuracoes } from './pages/Configuracoes';
import { Perfil as PaginaPerfil } from './pages/Perfil';
import { Auditoria } from './pages/Auditoria';
import { RecuperarSenha } from './pages/RecuperarSenha';
import { MeusPets } from './pages/MeusPets';
import { MinhaAgenda } from './pages/MinhaAgenda';

/**
 * RN-005: cada rota declara os perfis que podem alcançá-la. Quem não tem permissão
 * é levado para a página inicial do próprio perfil, em vez de ver uma tela de erro.
 * A API repete a verificação — esta camada é conveniência de navegação, não a defesa.
 */
function Protegida({ perfis, children }: { perfis?: Perfil[]; children: ReactNode }) {
  const { usuario, carregando } = useAuth();

  if (carregando) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Loader2 className="animate-spin text-brand" size={32} />
      </div>
    );
  }

  if (!usuario) {
    return <Navigate to="/login" replace />;
  }

  if (perfis && !perfis.includes(usuario.perfil)) {
    return <Navigate to={usuario.perfil === 'Tutor' ? '/meus-pets' : '/'} replace />;
  }

  return <Layout>{children}</Layout>;
}

/** O Tutor não tem painel operacional: sua página inicial é a lista de pets. */
function PaginaInicial() {
  const { usuario } = useAuth();

  if (usuario?.perfil === 'Tutor') {
    return <Navigate to="/meus-pets" replace />;
  }

  return <Dashboard />;
}

const EQUIPE: Perfil[] = ['Administrador', 'Veterinario', 'Apoio'];

export default function App() {
  return (
    <AuthProvider>
      {/* Uma única conexão com o mural de alterações serve todas as telas do portal. */}
      <AtualizacoesProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/recuperar-senha" element={<RecuperarSenha />} />

            <Route
              path="/"
              element={
                <Protegida>
                  <PaginaInicial />
                </Protegida>
              }
            />

            {/* HU-004 — agenda individual do veterinário */}
            <Route
              path="/agenda"
              element={
                <Protegida perfis={['Veterinario']}>
                  <Agenda />
                </Protegida>
              }
            />

            {/* HU-005 — agenda geral consolidada (RN-008) */}
            <Route
              path="/agenda-geral"
              element={
                <Protegida perfis={['Administrador', 'Apoio']}>
                  <AgendaGeral />
                </Protegida>
              }
            />

            {/* HU-003 — gestão de pacientes */}
            <Route
              path="/pacientes"
              element={
                <Protegida perfis={EQUIPE}>
                  <Pacientes />
                </Protegida>
              }
            />

            <Route
              path="/tutores"
              element={
                <Protegida perfis={EQUIPE}>
                  <Tutores />
                </Protegida>
              }
            />

            {/* HU-011 — prontuário; o tutor recebe a visão filtrada (RN-003) */}
            <Route
              path="/prontuario/:pacienteId"
              element={
                <Protegida>
                  <Prontuario />
                </Protegida>
              }
            />

            {/* HU-013 — área do tutor */}
            <Route
              path="/meus-pets"
              element={
                <Protegida perfis={['Tutor']}>
                  <MeusPets />
                </Protegida>
              }
            />

            {/* HU-006 — confirmação e cancelamento de presença */}
            <Route
              path="/minha-agenda"
              element={
                <Protegida perfis={['Tutor']}>
                  <MinhaAgenda />
                </Protegida>
              }
            />

            {/* HU-014 */}
            <Route
              path="/mensagens"
              element={
                <Protegida>
                  <Mensagens />
                </Protegida>
              }
            />

            {/* HU-015 */}
            <Route
              path="/notificacoes"
              element={
                <Protegida>
                  <Notificacoes />
                </Protegida>
              }
            />

            {/* HU-017 */}
            <Route
              path="/relatorios"
              element={
                <Protegida perfis={['Administrador', 'Veterinario']}>
                  <Relatorios />
                </Protegida>
              }
            />

            {/* HU-002 */}
            <Route
              path="/usuarios"
              element={
                <Protegida perfis={['Administrador']}>
                  <Usuarios />
                </Protegida>
              }
            />

            <Route
              path="/configuracoes"
              element={
                <Protegida perfis={['Administrador']}>
                  <Configuracoes />
                </Protegida>
              }
            />

            {/* Rastreabilidade dos acessos a dados clínicos. */}
            <Route
              path="/auditoria"
              element={
                <Protegida perfis={['Administrador']}>
                  <Auditoria />
                </Protegida>
              }
            />

            <Route
              path="/perfil"
              element={
                <Protegida>
                  <PaginaPerfil />
                </Protegida>
              }
            />

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </AtualizacoesProvider>
    </AuthProvider>
  );
}
