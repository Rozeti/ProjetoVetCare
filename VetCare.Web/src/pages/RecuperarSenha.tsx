import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ArrowLeft, KeyRound, Loader2, Mail, Stethoscope } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { Alerta } from '../components/ui';

/**
 * Redefinição de senha em duas etapas: o usuário pede o link e depois define a
 * nova senha com o token recebido. Chegando com ?token=… na URL, a tela já abre
 * na segunda etapa.
 */
export function RecuperarSenha() {
  const [parametros] = useSearchParams();
  const tokenDaUrl = parametros.get('token') ?? '';

  const [etapa, setEtapa] = useState<'solicitar' | 'redefinir'>(tokenDaUrl ? 'redefinir' : 'solicitar');
  const [email, setEmail] = useState('');
  const [token, setToken] = useState(tokenDaUrl);
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');

  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [concluido, setConcluido] = useState(false);
  const [enviando, setEnviando] = useState(false);

  async function solicitar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');
    setAviso('');
    setEnviando(true);

    try {
      const { data } = await api.post<{ mensagem: string; tokenDesenvolvimento?: string | null }>(
        '/api/usuarios/recuperar-senha',
        { email: email.trim() },
      );

      setAviso(data.mensagem);

      // Enquanto o envio por e-mail não está conectado, a API devolve o token em
      // desenvolvimento para que o fluxo possa ser concluído.
      if (data.tokenDesenvolvimento) {
        setToken(data.tokenDesenvolvimento);
        setEtapa('redefinir');
      }
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível processar a solicitação.'));
    } finally {
      setEnviando(false);
    }
  }

  async function redefinir(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (novaSenha.length < 6) {
      setErro('A nova senha deve ter no mínimo 6 caracteres.');
      return;
    }

    if (novaSenha !== confirmacao) {
      setErro('A confirmação não confere com a nova senha.');
      return;
    }

    setEnviando(true);

    try {
      await api.post('/api/usuarios/redefinir-senha', { token, novaSenha });
      setConcluido(true);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível redefinir a senha.'));
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
        </div>

        <div className="vc-card p-6 sm:p-8">
          {concluido ? (
            <div className="text-center">
              <div className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-full bg-sucesso-claro text-sucesso">
                <KeyRound size={24} />
              </div>

              <h2 className="text-lg font-bold text-slate-900">Senha redefinida</h2>
              <p className="mt-1 text-sm text-slate-500">Use a nova senha para entrar no sistema.</p>

              <Link to="/login" className="vc-botao-primario mt-6 w-full">
                Ir para o login
              </Link>
            </div>
          ) : etapa === 'solicitar' ? (
            <>
              <h2 className="mb-1 text-lg font-bold text-slate-900">Esqueceu a senha?</h2>
              <p className="mb-6 text-sm text-slate-500">
                Informe o e-mail da sua conta e enviaremos as instruções de redefinição.
              </p>

              <form onSubmit={solicitar} className="space-y-4" noValidate>
                <div>
                  <label htmlFor="email" className="vc-rotulo">
                    E-mail
                  </label>
                  <div className="relative">
                    <Mail
                      className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
                      size={18}
                    />
                    <input
                      id="email"
                      type="email"
                      className="vc-campo pl-10"
                      placeholder="seu@email.com"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      disabled={enviando}
                    />
                  </div>
                </div>

                {erro && <Alerta tipo="erro">{erro}</Alerta>}
                {aviso && <Alerta tipo="info">{aviso}</Alerta>}

                <button type="submit" className="vc-botao-primario w-full" disabled={enviando}>
                  {enviando && <Loader2 className="animate-spin" size={18} />}
                  Enviar instruções
                </button>
              </form>
            </>
          ) : (
            <>
              <h2 className="mb-1 text-lg font-bold text-slate-900">Definir nova senha</h2>
              <p className="mb-6 text-sm text-slate-500">O link de redefinição é válido por 30 minutos.</p>

              <form onSubmit={redefinir} className="space-y-4" noValidate>
                {!tokenDaUrl && (
                  <div>
                    <label htmlFor="token" className="vc-rotulo">
                      Código de redefinição
                    </label>
                    <input
                      id="token"
                      className="vc-campo font-mono text-xs"
                      value={token}
                      onChange={(e) => setToken(e.target.value)}
                      disabled={enviando}
                    />
                  </div>
                )}

                <div>
                  <label htmlFor="novaSenha" className="vc-rotulo">
                    Nova senha
                  </label>
                  <input
                    id="novaSenha"
                    type="password"
                    className="vc-campo"
                    value={novaSenha}
                    onChange={(e) => setNovaSenha(e.target.value)}
                    autoComplete="new-password"
                    disabled={enviando}
                  />
                </div>

                <div>
                  <label htmlFor="confirmacao" className="vc-rotulo">
                    Confirmar nova senha
                  </label>
                  <input
                    id="confirmacao"
                    type="password"
                    className="vc-campo"
                    value={confirmacao}
                    onChange={(e) => setConfirmacao(e.target.value)}
                    autoComplete="new-password"
                    disabled={enviando}
                  />
                </div>

                {erro && <Alerta tipo="erro">{erro}</Alerta>}

                <button type="submit" className="vc-botao-primario w-full" disabled={enviando}>
                  {enviando && <Loader2 className="animate-spin" size={18} />}
                  Redefinir senha
                </button>
              </form>
            </>
          )}

          {!concluido && (
            <Link
              to="/login"
              className="mt-6 flex items-center justify-center gap-1 text-sm font-medium text-brand hover:underline"
            >
              <ArrowLeft size={15} />
              Voltar para o login
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
