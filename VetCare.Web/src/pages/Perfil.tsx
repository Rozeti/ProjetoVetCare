import { useState, type FormEvent } from 'react';
import { Bell, KeyRound, Loader2, Mail, Phone, Smartphone } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import type { Usuario } from '../types';
import { Alerta, Avatar, CabecalhoPagina, Campo, Card } from '../components/ui';
import { formatarData, formatarDataHora } from '../utils/formato';

/** Dados da conta do usuário autenticado, preferências de notificação e troca da própria senha. */
export function Perfil() {
  const { usuario, ehTutor, atualizarUsuario } = useAuth();

  const [senhaAtual, setSenhaAtual] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erroSenha, setErroSenha] = useState('');
  const [avisoSenha, setAvisoSenha] = useState('');
  const [salvandoSenha, setSalvandoSenha] = useState(false);

  const [salvandoPreferencias, setSalvandoPreferencias] = useState(false);
  const [erroPreferencias, setErroPreferencias] = useState('');

  const [telefone, setTelefone] = useState(usuario?.telefone ?? '');
  const [endereco, setEndereco] = useState(usuario?.endereco ?? '');
  const [erroContato, setErroContato] = useState('');
  const [avisoContato, setAvisoContato] = useState('');
  const [salvandoContato, setSalvandoContato] = useState(false);

  if (!usuario) return null;

  async function alterarSenha(evento: FormEvent) {
    evento.preventDefault();
    setErroSenha('');
    setAvisoSenha('');

    if (novaSenha.length < 6) {
      setErroSenha('A nova senha deve ter no mínimo 6 caracteres.');
      return;
    }

    if (novaSenha !== confirmacao) {
      setErroSenha('A confirmação não confere com a nova senha.');
      return;
    }

    setSalvandoSenha(true);

    try {
      await api.put('/api/usuarios/me/senha', { senhaAtual, novaSenha });

      setAvisoSenha('Senha alterada com sucesso. Você também receberá um aviso por e-mail.');
      setSenhaAtual('');
      setNovaSenha('');
      setConfirmacao('');
    } catch (falha) {
      setErroSenha(mensagemDeErro(falha, 'Não foi possível alterar a senha.'));
    } finally {
      setSalvandoSenha(false);
    }
  }

  /** HU-015: o usuário decide se quer ser avisado também por e-mail e no celular. */
  async function alterarPreferencia(campo: 'notificarPorEmail' | 'notificarPorPush', valor: boolean) {
    if (!usuario) return;

    setErroPreferencias('');
    setSalvandoPreferencias(true);

    try {
      const { data } = await api.put<Usuario>('/api/usuarios/me/preferencias-de-notificacao', {
        notificarPorEmail: usuario.notificarPorEmail,
        notificarPorPush: usuario.notificarPorPush,
        [campo]: valor,
      });

      atualizarUsuario(data);
    } catch (falha) {
      setErroPreferencias(mensagemDeErro(falha, 'Não foi possível salvar a preferência.'));
    } finally {
      setSalvandoPreferencias(false);
    }
  }

  /** O próprio tutor mantém o contato em dia; é por ele que a clínica liga quando precisa. */
  async function salvarContato(evento: FormEvent) {
    evento.preventDefault();
    if (!usuario?.tutorId) return;

    setErroContato('');
    setAvisoContato('');
    setSalvandoContato(true);

    try {
      await api.put(`/api/tutores/${usuario.tutorId}`, { telefone, endereco });

      const { data } = await api.get<Usuario>('/api/usuarios/me');
      atualizarUsuario(data);
      setAvisoContato('Dados de contato atualizados.');
    } catch (falha) {
      setErroContato(mensagemDeErro(falha, 'Não foi possível atualizar o contato.'));
    } finally {
      setSalvandoContato(false);
    }
  }

  return (
    <>
      <CabecalhoPagina titulo="Minha conta" descricao="Seus dados de acesso, contato e preferências de aviso." />

      <div className="grid max-w-5xl gap-6 lg:grid-cols-2">
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

            {!ehTutor && usuario.telefone && (
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
            <Bell size={18} className="text-brand" />
            Como você quer ser avisado
          </h2>
          <p className="mb-5 text-sm text-slate-500">
            Os avisos sempre aparecem aqui no sistema. Escolha se quer recebê-los também por outros canais.
          </p>

          <div className="space-y-3">
            <OpcaoDeNotificacao
              icone={<Mail size={18} />}
              titulo="E-mail"
              descricao={`Enviado para ${usuario.email}.`}
              marcado={usuario.notificarPorEmail}
              desabilitado={salvandoPreferencias}
              aoAlternar={(valor) => alterarPreferencia('notificarPorEmail', valor)}
            />

            <OpcaoDeNotificacao
              icone={<Smartphone size={18} />}
              titulo="Celular"
              descricao="Notificação no aplicativo do VetCare, nos aparelhos em que você entrou."
              marcado={usuario.notificarPorPush}
              desabilitado={salvandoPreferencias}
              aoAlternar={(valor) => alterarPreferencia('notificarPorPush', valor)}
            />
          </div>

          {erroPreferencias && (
            <div className="mt-4">
              <Alerta tipo="erro">{erroPreferencias}</Alerta>
            </div>
          )}
        </Card>

        {ehTutor && usuario.tutorId && (
          <Card className="p-6">
            <h2 className="mb-1 flex items-center gap-2 font-semibold text-slate-900">
              <Phone size={18} className="text-brand" />
              Dados de contato
            </h2>
            <p className="mb-5 text-sm text-slate-500">
              Mantenha o telefone atualizado: é por ele que a clínica fala com você sobre o seu pet.
            </p>

            <form onSubmit={salvarContato} className="space-y-4">
              <Campo rotulo="Telefone">
                <input
                  className="vc-campo"
                  value={telefone}
                  onChange={(e) => setTelefone(e.target.value)}
                  placeholder="(61) 99999-0000"
                  autoComplete="tel"
                />
              </Campo>

              <Campo rotulo="Endereço">
                <input
                  className="vc-campo"
                  value={endereco}
                  onChange={(e) => setEndereco(e.target.value)}
                  autoComplete="street-address"
                />
              </Campo>

              {erroContato && <Alerta tipo="erro">{erroContato}</Alerta>}
              {avisoContato && <Alerta tipo="sucesso">{avisoContato}</Alerta>}

              <button type="submit" className="vc-botao-primario w-full" disabled={salvandoContato}>
                {salvandoContato && <Loader2 className="animate-spin" size={16} />}
                Salvar contato
              </button>
            </form>
          </Card>
        )}

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

            {erroSenha && <Alerta tipo="erro">{erroSenha}</Alerta>}
            {avisoSenha && <Alerta tipo="sucesso">{avisoSenha}</Alerta>}

            <button type="submit" className="vc-botao-primario w-full" disabled={salvandoSenha}>
              {salvandoSenha && <Loader2 className="animate-spin" size={16} />}
              Alterar senha
            </button>
          </form>
        </Card>
      </div>
    </>
  );
}

function OpcaoDeNotificacao({
  icone,
  titulo,
  descricao,
  marcado,
  desabilitado,
  aoAlternar,
}: {
  icone: React.ReactNode;
  titulo: string;
  descricao: string;
  marcado: boolean;
  desabilitado: boolean;
  aoAlternar: (valor: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 px-4 py-3 transition hover:bg-slate-50">
      <span className="text-brand">{icone}</span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-semibold text-slate-800">{titulo}</span>
        <span className="block text-xs text-slate-500">{descricao}</span>
      </span>
      <input
        type="checkbox"
        role="switch"
        aria-checked={marcado}
        className="h-5 w-5 rounded accent-brand"
        checked={marcado}
        disabled={desabilitado}
        onChange={(e) => aoAlternar(e.target.checked)}
      />
    </label>
  );
}
