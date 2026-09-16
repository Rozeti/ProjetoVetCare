import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Loader2, Lock, Mail, Stethoscope } from 'lucide-react';
import { useAuth } from '../contexts/auth';
import { mensagemDeErro } from '../services/api';
import { Alerta } from '../components/ui';

/** HU-001: tela de login única para todos os perfis. */
export function Login() {
  const { usuario, entrar, carregando } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState('');
  const [enviando, setEnviando] = useState(false);

  if (carregando) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <Loader2 className="animate-spin text-brand" size={32} />
      </div>
    );
  }

  // HU-001, CA-1: cada perfil cai no painel correspondente.
  if (usuario) {
    return <Navigate to={usuario.perfil === 'Tutor' ? '/meus-pets' : '/'} replace />;
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!email.trim() || !senha) {
      setErro('Informe o e-mail e a senha para entrar.');
      return;
    }

    setEnviando(true);

    try {
      await entrar(email.trim(), senha);
      navigate('/', { replace: true });
    } catch (falha) {
      // HU-001, CA-2/CA-3/CA-5: a API decide a mensagem — genérica para credenciais
      // inválidas, específica para conta inativa ou bloqueio por tentativas.
      setErro(mensagemDeErro(falha, 'Não foi possível entrar. Tente novamente.'));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-brand-50 via-slate-50 to-slate-100 px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <div className="mb-3 inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-brand text-white shadow-lg shadow-brand/25">
            <Stethoscope size={32} />
          </div>
          <h1 className="font-display text-3xl font-bold text-slate-900">VetCare</h1>
          <p className="mt-1 text-sm text-slate-500">Gestão clínica da Clínica VetSPA</p>
        </div>

        <div className="vc-card p-6 sm:p-8">
          <h2 className="mb-1 text-lg font-bold text-slate-900">Entrar no sistema</h2>
          <p className="mb-6 text-sm text-slate-500">
            Use as credenciais fornecidas pela administração da clínica.
          </p>

          <form onSubmit={aoEnviar} className="space-y-4" noValidate>
            <div>
              <label htmlFor="email" className="vc-rotulo">
                E-mail
              </label>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
                <input
                  id="email"
                  type="email"
                  className="vc-campo pl-10"
                  placeholder="seu@email.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  autoComplete="username"
                  disabled={enviando}
                />
              </div>
            </div>

            <div>
              <label htmlFor="senha" className="vc-rotulo">
                Senha
              </label>
              <div className="relative">
                <Lock className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
                <input
                  id="senha"
                  type="password"
                  className="vc-campo pl-10"
                  placeholder="••••••••"
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                  autoComplete="current-password"
                  disabled={enviando}
                />
              </div>
            </div>

            {erro && <Alerta tipo="erro">{erro}</Alerta>}

            <button type="submit" className="vc-botao-primario w-full" disabled={enviando}>
              {enviando ? (
                <>
                  <Loader2 className="animate-spin" size={18} />
                  Entrando...
                </>
              ) : (
                'Entrar'
              )}
            </button>
          </form>

          <Link
            to="/recuperar-senha"
            className="mt-5 block text-center text-sm font-medium text-brand hover:underline"
          >
            Esqueci minha senha
          </Link>
        </div>
      </div>
    </div>
  );
}
