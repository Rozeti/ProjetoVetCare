import { useEffect, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import {
  BarChart3,
  Bell,
  CalendarDays,
  CalendarRange,
  FileText,
  LayoutDashboard,
  LogOut,
  Menu,
  MessageSquare,
  PawPrint,
  ScrollText,
  Settings,
  Stethoscope,
  Users,
  UserSquare2,
  X,
} from 'lucide-react';
import { useAuth } from '../contexts/auth';
import { api } from '../services/api';
import type { Perfil } from '../types';
import { Avatar } from './ui';

interface ItemMenu {
  rotulo: string;
  caminho: string;
  icone: typeof LayoutDashboard;
  /** RN-005: o item só aparece para os perfis autorizados. */
  perfis: Perfil[];
}

const MENU: ItemMenu[] = [
  { rotulo: 'Painel', caminho: '/', icone: LayoutDashboard, perfis: ['Administrador', 'Veterinario', 'Apoio'] },
  { rotulo: 'Meus pets', caminho: '/meus-pets', icone: PawPrint, perfis: ['Tutor'] },
  { rotulo: 'Minha agenda', caminho: '/minha-agenda', icone: CalendarDays, perfis: ['Tutor'] },
  { rotulo: 'Agenda', caminho: '/agenda', icone: CalendarDays, perfis: ['Veterinario'] },
  { rotulo: 'Agenda geral', caminho: '/agenda-geral', icone: CalendarRange, perfis: ['Administrador', 'Apoio'] },
  { rotulo: 'Pacientes', caminho: '/pacientes', icone: PawPrint, perfis: ['Administrador', 'Veterinario', 'Apoio'] },
  { rotulo: 'Tutores', caminho: '/tutores', icone: UserSquare2, perfis: ['Administrador', 'Veterinario', 'Apoio'] },
  { rotulo: 'Mensagens', caminho: '/mensagens', icone: MessageSquare, perfis: ['Administrador', 'Veterinario', 'Tutor', 'Apoio'] },
  { rotulo: 'Notificações', caminho: '/notificacoes', icone: Bell, perfis: ['Administrador', 'Veterinario', 'Tutor', 'Apoio'] },
  { rotulo: 'Relatórios', caminho: '/relatorios', icone: BarChart3, perfis: ['Administrador', 'Veterinario'] },
  { rotulo: 'Usuários', caminho: '/usuarios', icone: Users, perfis: ['Administrador'] },
  { rotulo: 'Auditoria', caminho: '/auditoria', icone: ScrollText, perfis: ['Administrador'] },
  { rotulo: 'Configurações', caminho: '/configuracoes', icone: Settings, perfis: ['Administrador'] },
];

const NOME_DO_PERFIL: Record<Perfil, string> = {
  Administrador: 'Administrador',
  Veterinario: 'Veterinário(a)',
  Tutor: 'Tutor(a)',
  Apoio: 'Apoio administrativo',
};

export function Layout({ children }: { children: React.ReactNode }) {
  const { usuario, sair } = useAuth();
  const navigate = useNavigate();
  const local = useLocation();

  const [naoLidas, setNaoLidas] = useState(0);
  const [notificacoes, setNotificacoes] = useState(0);

  // Guardar a rota em que o menu foi aberto, em vez de um booleano, faz o menu lateral
  // do celular se fechar sozinho ao navegar — sem precisar de um efeito para isso.
  const [menuAbertoEm, setMenuAbertoEm] = useState<string | null>(null);
  const menuAberto = menuAbertoEm === local.pathname;

  // HU-015: os contadores do topo acompanham as notificações sem recarregar a página.
  useEffect(() => {
    let ativo = true;

    async function carregarContadores() {
      try {
        const [mensagens, avisos] = await Promise.all([
          api.get<number>('/api/mensagens/nao-lidas'),
          api.get<number>('/api/notificacoes/nao-visualizadas'),
        ]);

        if (ativo) {
          setNaoLidas(mensagens.data);
          setNotificacoes(avisos.data);
        }
      } catch {
        // Os contadores são informativos: uma falha aqui não deve atrapalhar a navegação.
      }
    }

    carregarContadores();

    const intervalo = window.setInterval(carregarContadores, 30_000);

    return () => {
      ativo = false;
      window.clearInterval(intervalo);
    };
  }, [local.pathname]);

  if (!usuario) {
    return null;
  }

  const itens = MENU.filter((item) => item.perfis.includes(usuario.perfil));

  function aoSair() {
    sair();
    navigate('/login', { replace: true });
  }

  return (
    <div className="min-h-screen bg-slate-50">
      {/* RNF-001: sidebar fixa como elemento central de navegação. */}
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-slate-200 bg-white
                    transition-transform lg:translate-x-0 ${menuAberto ? 'translate-x-0' : '-translate-x-full'}`}
      >
        <div className="flex h-16 items-center gap-2 border-b border-slate-200 px-5">
          <Stethoscope className="text-brand" size={26} />
          <span className="font-display text-xl font-bold text-slate-900">VetCare</span>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3" aria-label="Navegação principal">
          {itens.map((item) => {
            const Icone = item.icone;
            const contador =
              item.caminho === '/mensagens' ? naoLidas : item.caminho === '/notificacoes' ? notificacoes : 0;

            return (
              <NavLink
                key={item.caminho}
                to={item.caminho}
                end={item.caminho === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition ${
                    isActive ? 'bg-brand-100 text-brand-dark' : 'text-slate-600 hover:bg-slate-100'
                  }`
                }
              >
                <Icone size={18} />
                <span className="flex-1">{item.rotulo}</span>
                {contador > 0 && (
                  <span className="rounded-full bg-perigo px-1.5 py-0.5 text-[10px] font-bold text-white">
                    {contador > 99 ? '99+' : contador}
                  </span>
                )}
              </NavLink>
            );
          })}
        </nav>

        <div className="border-t border-slate-200 p-3">
          <Link
            to="/perfil"
            className="flex items-center gap-3 rounded-xl p-2 transition hover:bg-slate-100"
          >
            <Avatar nome={usuario.nome} />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-slate-900">{usuario.nome}</p>
              <p className="truncate text-xs text-slate-500">{NOME_DO_PERFIL[usuario.perfil]}</p>
            </div>
          </Link>

          <button
            type="button"
            onClick={aoSair}
            className="mt-1 flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-600 transition hover:bg-perigo-claro hover:text-red-700"
          >
            <LogOut size={18} />
            Sair do sistema
          </button>
        </div>
      </aside>

      {menuAberto && (
        <div
          className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden"
          onClick={() => setMenuAbertoEm(null)}
          aria-hidden="true"
        />
      )}

      <div className="lg:pl-64">
        {/* RNF-006: no celular a sidebar vira menu retrátil. */}
        <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-slate-200 bg-white/90 px-4 backdrop-blur lg:hidden">
          <button
            type="button"
            onClick={() => setMenuAbertoEm((aberta) => (aberta === local.pathname ? null : local.pathname))}
            className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
            aria-label={menuAberto ? 'Fechar menu' : 'Abrir menu'}
          >
            {menuAberto ? <X size={20} /> : <Menu size={20} />}
          </button>

          <Stethoscope className="text-brand" size={22} />
          <span className="font-display text-lg font-bold text-slate-900">VetCare</span>

          <Link to="/notificacoes" className="relative ml-auto rounded-lg p-2 text-slate-600 hover:bg-slate-100">
            <Bell size={20} />
            {notificacoes > 0 && (
              <span className="absolute right-1 top-1 h-2 w-2 rounded-full bg-perigo" aria-hidden="true" />
            )}
          </Link>
        </header>

        <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">{children}</main>

        <footer className="mx-auto max-w-7xl px-4 pb-8 text-center text-xs text-slate-400 sm:px-6 lg:px-8">
          <FileText size={12} className="mr-1 inline" />
          VetCare — plataforma de gestão clínica veterinária da Clínica VetSPA
        </footer>
      </div>
    </div>
  );
}
