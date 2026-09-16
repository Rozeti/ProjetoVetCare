import { useState, type FormEvent } from 'react';
import { KeyRound, Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import { Alerta, Avatar, CabecalhoPagina, Campo, Card } from '../components/ui';
import { formatarData, formatarDataHora } from '../utils/formato';

/** Dados da conta do usuário autenticado e troca da própria senha. */
export function Perfil() {
  const { usuario } = useAuth();

  const [senhaAtual, setSenhaAtual] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [salvando, setSalvando] = useState(false);

  if (!usuario) return null;

  async function alterarSenha(evento: FormEvent) {
    evento.preventDefault();
    setErro('');
    setAviso('');

    if (novaSenha.length < 6) {
      setErro('A nova senha deve ter no mínimo 6 caracteres.');
      return;
    }

    if (novaSenha !== confirmacao) {
      setErro('A confirmação não confere com a nova senha.');
      return;
    }

    setSalvando(true);

    try {
      await api.put('/api/usuarios/me/senha', { senhaAtual, novaSenha });

      setAviso('Senha alterada com sucesso.');
      setSenhaAtual('');
      setNovaSenha('');
      setConfirmacao('');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível alterar a senha.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <>
      <CabecalhoPagina titulo="Minha conta" descricao="Seus dados de acesso ao VetCare." />

      <div className="grid max-w-4xl gap-6 lg:grid-cols-2">
        <Card className="p-6">
          <div className="mb-5 flex items-center gap-4">
            <Avatar nome={usuario.nome} />
            <div className="min-w-0">
              <p className="truncate text-lg font-bold text-slate-900">{usuario.nome}</p>
              <p className="truncate text-sm text-slate-500">{usuario.email}</p>
            </div>
          </div>

          <dl className="space-y-3 text-sm">
            <div className="flex justify-between gap-3 border-t border-slate-100 pt-3">
              <dt className="text-slate-500">Perfil</dt>
              <dd className="font-medium text-slate-800">{usuario.perfil}</dd>
            </div>

            {usuario.crmv && (
              <div className="flex justify-between gap-3 border-t border-slate-100 pt-3">
                <dt className="text-slate-500">CRMV</dt>
                <dd className="font-medium text-slate-800">{usuario.crmv}</dd>
              </div>
            )}

            {usuario.especialidade && (
              <div className="flex justify-between gap-3 border-t border-slate-100 pt-3">
                <dt className="text-slate-500">Especialidade</dt>
                <dd className="font-medium text-slate-800">{usuario.especialidade}</dd>
              </div>
            )}

            {usuario.telefone && (
              <div className="flex justify-between gap-3 border-t border-slate-100 pt-3">
                <dt className="text-slate-500">Telefone</dt>
                <dd className="font-medium text-slate-800">{usuario.telefone}</dd>
              </div>
            )}

            <div className="flex justify-between gap-3 border-t border-slate-100 pt-3">
              <dt className="text-slate-500">Cadastro</dt>
              <dd className="font-medium text-slate-800">{formatarData(usuario.dataCadastro)}</dd>
            </div>

            {usuario.ultimoAcesso && (
              <div className="flex justify-between gap-3 border-t border-slate-100 pt-3">
                <dt className="text-slate-500">Último acesso</dt>
                <dd className="font-medium text-slate-800">{formatarDataHora(usuario.ultimoAcesso)}</dd>
              </div>
            )}
          </dl>
        </Card>

        <Card className="p-6">
          <h2 className="mb-1 flex items-center gap-2 font-semibold text-slate-900">
            <KeyRound size={18} className="text-brand" />
            Alterar senha
          </h2>
          <p className="mb-5 text-sm text-slate-500">
            Se você entrou com uma senha provisória, troque-a agora por uma de sua escolha.
          </p>

          <form onSubmit={alterarSenha} className="space-y-4">
            <Campo rotulo="Senha atual" obrigatorio>
              <input
                type="password"
                className="vc-campo"
                value={senhaAtual}
                onChange={(e) => setSenhaAtual(e.target.value)}
                autoComplete="current-password"
              />
            </Campo>

            <Campo rotulo="Nova senha" obrigatorio dica="Mínimo de 6 caracteres.">
              <input
                type="password"
                className="vc-campo"
                value={novaSenha}
                onChange={(e) => setNovaSenha(e.target.value)}
                autoComplete="new-password"
              />
            </Campo>

            <Campo rotulo="Confirmar nova senha" obrigatorio>
              <input
                type="password"
                className="vc-campo"
                value={confirmacao}
                onChange={(e) => setConfirmacao(e.target.value)}
                autoComplete="new-password"
              />
            </Campo>

            {erro && <Alerta tipo="erro">{erro}</Alerta>}
            {aviso && <Alerta tipo="sucesso">{aviso}</Alerta>}

            <button type="submit" className="vc-botao-primario w-full" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              Alterar senha
            </button>
          </form>
        </Card>
      </div>
    </>
  );
}
