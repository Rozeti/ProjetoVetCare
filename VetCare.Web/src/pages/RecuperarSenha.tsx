import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ArrowLeft, KeyRound, Loader2, Mail, MailCheck, Stethoscope } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import type { RespostaRecuperacao } from '../types';
import { Alerta } from '../components/ui';

interface Props {
  /** Conta recém-criada pela clínica: o texto fala em criar a senha, não em recuperá-la. */
  primeiroAcesso?: boolean;
}

type Etapa = 'solicitar' | 'redefinir';

/**
 * "Esqueci minha senha" e primeiro acesso, em duas etapas. A pessoa informa o e-mail
 * cadastrado e recebe um link (que abre esta tela já com o token) e um código de seis
 * dígitos (para quem prefere digitar, ou está no aplicativo). Qualquer um dos dois
 * define a nova senha, uma única vez e dentro do prazo.
 */
export function RecuperarSenha({ primeiroAcesso = false }: Props) {
  const [parametros] = useSearchParams();
  const tokenDaUrl = parametros.get('token') ?? '';

  const [etapa, setEtapa] = useState<Etapa>(tokenDaUrl ? 'redefinir' : 'solicitar');
  const [email, setEmail] = useState('');
  const [codigo, setCodigo] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');

  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [codigoPreenchidoEmDesenvolvimento, setCodigoPreenchidoEmDesenvolvimento] = useState(false);
  const [mensagemFinal, setMensagemFinal] = useState('');
  const [enviando, setEnviando] = useState(false);

  const usaToken = !!tokenDaUrl;

  async function solicitar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');
    setAviso('');

    if (!email.trim()) {
      setErro('Informe o e-mail da sua conta.');
      return;
    }

    setEnviando(true);

    try {
      const { data } = await api.post<RespostaRecuperacao>('/api/usuarios/recuperar-senha', {
        email: email.trim(),
      });

      setAviso(data.mensagem);

      // Sem servidor de e-mail configurado, a API de desenvolvimento devolve o código
      // na resposta para o fluxo poder ser percorrido de ponta a ponta.
      if (data.codigoDesenvolvimento) {
        setCodigo(data.codigoDesenvolvimento);
        setCodigoPreenchidoEmDesenvolvimento(true);
      }

      setEtapa('redefinir');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível processar a solicitação.'));
    } finally {
      setEnviando(false);
    }
  }

  async function redefinir(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!usaToken && codigo.replace(/\D/g, '').length !== 6) {
      setErro('Digite o código de 6 dígitos recebido por e-mail.');
      return;
    }

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
      const corpo = usaToken
        ? { token: tokenDaUrl, novaSenha }
        : { email: email.trim(), codigo: codigo.replace(/\D/g, ''), novaSenha };

      const { data } = await api.post<{ mensagem: string }>('/api/usuarios/redefinir-senha', corpo);

      setMensagemFinal(data.mensagem || 'Senha redefinida com sucesso.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível redefinir a senha.'));
    } finally {
      setEnviando(false);
    }
  }

  function voltarParaSolicitar() {
    setEtapa('solicitar');
    setErro('');
    setAviso('');
    setCodigo('');
    setCodigoPreenchidoEmDesenvolvimento(false);
  }

  const tituloDaEtapaInicial = primeiroAcesso ? 'Crie sua senha' : 'Esqueceu a senha?';
  const tituloDaEtapaFinal = primeiroAcesso ? 'Crie sua senha' : 'Definir nova senha';

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
          {mensagemFinal ? (
            <div className="text-center">
              <div className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-full bg-sucesso-claro text-sucesso">
                <KeyRound size={24} />
              </div>

              <h2 className="text-lg font-bold text-slate-900">{primeiroAcesso ? 'Senha criada' : 'Senha redefinida'}</h2>
              <p className="mt-1 text-sm text-slate-500">{mensagemFinal}</p>

              <Link to="/login" className="vc-botao-primario mt-6 w-full">
                Ir para o login
              </Link>
            </div>
          ) : etapa === 'solicitar' ? (
            <>
              <h2 className="mb-1 text-lg font-bold text-slate-900">{tituloDaEtapaInicial}</h2>
              <p className="mb-6 text-sm text-slate-500">
                Informe o e-mail da sua conta. Você receberá um link e um código de 6 dígitos para
                {primeiroAcesso ? ' criar a sua senha.' : ' definir uma nova senha.'}
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
                      autoComplete="username"
                      disabled={enviando}
                    />
                  </div>
                </div>

                {erro && <Alerta tipo="erro">{erro}</Alerta>}

                <button type="submit" className="vc-botao-primario w-full" disabled={enviando}>
                  {enviando && <Loader2 className="animate-spin" size={18} />}
                  Enviar instruções
                </button>
              </form>
            </>
          ) : (
            <>
              <h2 className="mb-1 text-lg font-bold text-slate-900">{tituloDaEtapaFinal}</h2>

              {usaToken ? (
                <p className="mb-6 text-sm text-slate-500">
                  Escolha a sua nova senha. Este link é de uso único e tem prazo de validade.
                </p>
              ) : (
                <div className="mb-6 flex items-start gap-3 rounded-xl bg-brand-50 px-4 py-3 text-sm text-brand-dark">
                  <MailCheck size={18} className="mt-0.5 shrink-0" />
                  <p>
                    {aviso || 'Se houver uma conta com este e-mail, enviamos as instruções.'}{' '}
                    Abra o e-mail e digite abaixo o código de 6 dígitos, ou clique no link da mensagem.
                  </p>
                </div>
              )}

              <form onSubmit={redefinir} className="space-y-4" noValidate>
                {!usaToken && (
                  <div>
                    <label htmlFor="codigo" className="vc-rotulo">
                      Código recebido por e-mail
                    </label>
                    <input
                      id="codigo"
                      inputMode="numeric"
                      autoComplete="one-time-code"
                      maxLength={7}
                      className="vc-campo text-center font-mono text-lg tracking-[0.4em]"
                      placeholder="000000"
                      value={codigo}
                      onChange={(e) => setCodigo(e.target.value)}
                      disabled={enviando}
                    />
                    {codigoPreenchidoEmDesenvolvimento && (
                      <p className="mt-1 text-xs text-slate-500">
                        Ambiente de desenvolvimento sem servidor de e-mail: o código foi preenchido automaticamente.
                      </p>
                    )}
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
                  <p className="mt-1 text-xs text-slate-500">Mínimo de 6 caracteres.</p>
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
                  {primeiroAcesso ? 'Criar senha' : 'Redefinir senha'}
                </button>
              </form>

              {!usaToken && (
                <button
                  type="button"
                  onClick={voltarParaSolicitar}
                  className="mt-4 block w-full text-center text-sm font-medium text-brand hover:underline"
                  disabled={enviando}
                >
                  Não recebeu? Enviar novamente
                </button>
              )}
            </>
          )}

          {!mensagemFinal && (
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
