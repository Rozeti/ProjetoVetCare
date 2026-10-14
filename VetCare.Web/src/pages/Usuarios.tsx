import { useCallback, useState, type FormEvent } from 'react';
import { KeyRound, Loader2, Pencil, Plus, Power, Search, Users } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useConfirmacao } from '../hooks/useConfirmacao';
import { useAuth } from '../contexts/auth';
import type { PaginaDe, Perfil, Usuario } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando, Etiqueta, Modal, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { emailValido, formatarData } from '../utils/formato';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { paginaVazia } from '../utils/paginacao';

const PERFIS: { valor: Perfil; rotulo: string }[] = [
  { valor: 'Administrador', rotulo: 'Administrador' },
  { valor: 'Veterinario', rotulo: 'Veterinário(a)' },
  { valor: 'Tutor', rotulo: 'Tutor(a)' },
  { valor: 'Apoio', rotulo: 'Apoio administrativo' },
];

const ESTILO_PERFIL: Record<Perfil, string> = {
  Administrador: 'bg-info-claro text-purple-800',
  Veterinario: 'bg-brand-100 text-brand-dark',
  Tutor: 'bg-sucesso-claro text-emerald-800',
  Apoio: 'bg-alerta-claro text-amber-800',
};

const FORM_VAZIO = {
  nome: '',
  email: '',
  senha: '',
  perfil: 'Veterinario' as Perfil,
  crmv: '',
  especialidade: '',
  telefone: '',
  endereco: '',
  cpf: '',
  setor: '',
};

/** HU-002: cadastro, edição, ativação/desativação e redefinição de senha dos usuários. */
export function Usuarios() {
  const { usuario: usuarioLogado, temPerfil } = useAuth();
  // O apoio consulta a equipe (contatos, vínculos); só a administração altera cadastros.
  const ehAdministrador = temPerfil('Administrador');

  const [numeroPagina, setNumeroPagina] = useState(1);
  const [busca, setBusca] = useState('');
  const [filtroPerfil, setFiltroPerfil] = useState('');
  const [aviso, setAviso] = useState('');

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<Usuario | null>(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [erroForm, setErroForm] = useState('');
  const [salvando, setSalvando] = useState(false);

  const [senhaGerada, setSenhaGerada] = useState<{ email: string; senha: string } | null>(null);

  const buscar = useCallback(async () => {
    const { data } = await api.get<PaginaDe<Usuario>>('/api/usuarios', {
      params: {
        busca: busca || undefined,
        perfil: filtroPerfil || undefined,
        pagina: numeroPagina,
        tamanho: 20,
      },
    });

    return data;
  }, [busca, filtroPerfil, numeroPagina]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar os usuários.',
    300,
  );

  useAtualizacao(['usuarios', 'veterinarios', 'tutores'], recarregar);

  const pagina = dados ?? paginaVazia<Usuario>();

  // Um filtro novo invalida a página atual: o resultado pode ter menos páginas.
  function filtrar(aplicar: () => void) {
    aplicar();
    setNumeroPagina(1);
  }

  function abrirNovo() {
    setEmEdicao(null);
    setForm(FORM_VAZIO);
    setErroForm('');
    setModalAberto(true);
  }

  function abrirEdicao(usuario: Usuario) {
    setEmEdicao(usuario);
    setForm({
      ...FORM_VAZIO,
      nome: usuario.nome,
      email: usuario.email,
      perfil: usuario.perfil,
      crmv: usuario.crmv ?? '',
      especialidade: usuario.especialidade ?? '',
      telefone: usuario.telefone ?? '',
      endereco: usuario.endereco ?? '',
      cpf: usuario.cpf ?? '',
      setor: usuario.setor ?? '',
    });
    setErroForm('');
    setModalAberto(true);
  }

  const { confirmar, confirmarEdicao } = useConfirmacao();

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErroForm('');

    // As mesmas regras da API (CriarUsuarioDTO / AtualizarUsuarioDTO), conferidas antes do envio.
    const nome = form.nome.trim();
    const email = form.email.trim();

    if (nome.length < 3 || nome.length > 120) {
      setErroForm('O nome deve ter entre 3 e 120 caracteres.');
      return;
    }

    if (!emailValido(email)) {
      setErroForm('Informe um e-mail válido.');
      return;
    }

    if (!emEdicao && form.senha && (form.senha.length < 6 || form.senha.length > 64)) {
      setErroForm('A senha inicial deve ter entre 6 e 64 caracteres, ou ficar em branco.');
      return;
    }

    if (form.perfil === 'Veterinario' && form.crmv.trim().length < 3) {
      setErroForm('Informe o CRMV do veterinário.');
      return;
    }

    if (
      emEdicao &&
      !(await confirmarEdicao(
        <>
          Salvar as alterações no cadastro de <strong>{form.nome}</strong>?
        </>,
      ))
    ) {
      return;
    }

    setSalvando(true);

    try {
      if (emEdicao) {
        await api.put(`/api/usuarios/${emEdicao.id}`, {
          nome: form.nome,
          email: form.email,
          crmv: form.crmv || null,
          especialidade: form.especialidade || null,
          telefone: form.telefone || null,
          endereco: form.endereco || null,
          cpf: form.cpf || null,
          setor: form.setor || null,
        });

        setAviso('Usuário atualizado com sucesso.');
      } else {
        await api.post('/api/usuarios', {
          ...form,
          nome,
          email,
          senha: form.senha || null,
        });

        setAviso(
          form.senha
            ? 'Usuário cadastrado com sucesso. Ele recebeu um e-mail de boas-vindas.'
            : 'Usuário cadastrado. Ele recebeu por e-mail o link para criar a própria senha.',
        );
      }

      setModalAberto(false);
      recarregar();
    } catch (falha) {
      // RN-007: e-mail duplicado chega aqui como conflito.
      setErroForm(mensagemDeErro(falha, 'Não foi possível salvar o usuário.'));
    } finally {
      setSalvando(false);
    }
  }

  /** HU-002, CA-4: desativar impede novos acessos sem excluir os dados associados. */
  async function alternarStatus(usuario: Usuario) {
    setErro('');

    if (
      usuario.ativo &&
      !(await confirmar({
        titulo: 'Desativar usuário',
        mensagem: (
          <>
            Desativar o acesso de <strong>{usuario.nome}</strong>? A pessoa deixa de entrar no sistema em até um
            minuto, e os dados associados a ela são mantidos.
          </>
        ),
        rotuloConfirmar: 'Desativar',
        perigo: true,
      }))
    ) {
      return;
    }

    try {
      await api.patch(`/api/usuarios/${usuario.id}/status`, { ativo: !usuario.ativo });
      setAviso(usuario.ativo ? `${usuario.nome} foi desativado.` : `${usuario.nome} foi reativado.`);
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível alterar o status do usuário.'));
    }
  }

  /** HU-002, CA-5: o sistema gera uma nova senha provisória e a envia ao usuário por e-mail. */
  async function redefinirSenha(usuario: Usuario) {
    const confirmado = await confirmar({
      titulo: 'Redefinir senha',
      mensagem: (
        <>
          Gerar uma nova senha provisória para <strong>{usuario.nome}</strong>? A senha atual deixa de funcionar
          imediatamente, e a nova é enviada por e-mail à pessoa.
        </>
      ),
      rotuloConfirmar: 'Gerar nova senha',
      perigo: true,
    });

    if (!confirmado) {
      return;
    }

    setErro('');

    try {
      const { data } = await api.post<{ email: string; senhaProvisoria: string }>(
        `/api/usuarios/${usuario.id}/redefinir-senha`,
      );

      setSenhaGerada({ email: data.email, senha: data.senhaProvisoria });
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível redefinir a senha.'));
    }
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Usuários"
        descricao="Controle de acesso da clínica por perfil."
        acoes={
          ehAdministrador ? (
            <button type="button" className="vc-botao-primario" onClick={abrirNovo}>
              <Plus size={16} />
              Novo usuário
            </button>
          ) : undefined
        }
      />

      {erro && (
        <div className="mb-4">
          <Alerta tipo="erro" aoFechar={() => setErro('')}>
            {erro}
          </Alerta>
        </div>
      )}
      {aviso && (
        <div className="mb-4">
          <Alerta tipo="sucesso" aoFechar={() => setAviso('')}>
            {aviso}
          </Alerta>
        </div>
      )}

      <Card className="mb-6 p-4">
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-64 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
            <input
              className="vc-campo pl-10"
              placeholder="Buscar por nome ou e-mail"
              value={busca}
              onChange={(e) => filtrar(() => setBusca(e.target.value))}
              aria-label="Buscar usuários"
            />
          </div>

          <select
            className="vc-campo w-auto"
            value={filtroPerfil}
            onChange={(e) => filtrar(() => setFiltroPerfil(e.target.value))}
            aria-label="Filtrar por perfil"
          >
            <option value="">Todos os perfis</option>
            {PERFIS.map((perfil) => (
              <option key={perfil.valor} value={perfil.valor}>
                {perfil.rotulo}
              </option>
            ))}
          </select>
        </div>
      </Card>

      {carregando ? (
        <Carregando texto="Carregando usuários..." />
      ) : pagina.itens.length === 0 ? (
        <Card>
          <SemDados icone={<Users size={40} />} titulo="Nenhum usuário encontrado" />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th scope="col">Nome</th>
                  <th scope="col">E-mail</th>
                  <th scope="col">Perfil</th>
                  <th scope="col">Vínculo</th>
                  <th scope="col">Cadastro</th>
                  <th scope="col">Status</th>
                  {ehAdministrador && (
                    <th scope="col" className="text-right">
                      Ações
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((usuario) => (
                  <tr key={usuario.id}>
                    <td className="font-medium text-slate-900">{usuario.nome}</td>
                    <td className="text-slate-600">{usuario.email}</td>
                    <td>
                      <Etiqueta className={ESTILO_PERFIL[usuario.perfil]}>{usuario.perfil}</Etiqueta>
                    </td>
                    <td className="text-xs text-slate-500">
                      {usuario.crmv && <span className="block">{usuario.crmv}</span>}
                      {usuario.especialidade && <span className="block">{usuario.especialidade}</span>}
                      {usuario.telefone && <span className="block">{usuario.telefone}</span>}
                      {!usuario.crmv && !usuario.telefone && '—'}
                    </td>
                    <td className="text-xs text-slate-500">{formatarData(usuario.dataCadastro)}</td>
                    <td>
                      <Etiqueta className={usuario.ativo ? 'bg-sucesso-claro text-emerald-800' : 'bg-slate-200 text-slate-600'}>
                        {usuario.ativo ? 'Ativo' : 'Inativo'}
                      </Etiqueta>
                    </td>
                    {ehAdministrador && (
                      <td>
                        <div className="flex items-center justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => abrirEdicao(usuario)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                            title="Editar dados"
                            aria-label={`Editar ${usuario.nome}`}
                          >
                            <Pencil size={16} />
                          </button>

                          {/* A própria senha se troca em Perfil, com a senha atual; aqui a sessão cairia na hora. */}
                          {usuario.id !== usuarioLogado?.id && (
                            <button
                              type="button"
                              onClick={() => redefinirSenha(usuario)}
                              className="rounded-lg p-2 text-slate-500 hover:bg-brand-100 hover:text-brand"
                              title="Redefinir senha"
                              aria-label={`Redefinir a senha de ${usuario.nome}`}
                            >
                              <KeyRound size={16} />
                            </button>
                          )}

                          {/* O administrador logado não pode desativar a própria conta. */}
                          {usuario.id !== usuarioLogado?.id && (
                            <button
                              type="button"
                              onClick={() => alternarStatus(usuario)}
                              className={`rounded-lg p-2 ${
                                usuario.ativo
                                  ? 'text-slate-500 hover:bg-perigo-claro hover:text-perigo'
                                  : 'text-sucesso hover:bg-sucesso-claro'
                              }`}
                              title={usuario.ativo ? 'Desativar acesso' : 'Reativar acesso'}
                              aria-label={`${usuario.ativo ? 'Desativar' : 'Reativar'} o acesso de ${usuario.nome}`}
                            >
                              <Power size={16} />
                            </button>
                          )}
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="usuários" />
        </Card>
      )}

      <Modal
        aberto={modalAberto}
        titulo={emEdicao ? 'Editar usuário' : 'Novo usuário'}
        descricao={
          emEdicao
            ? 'A atualização preserva o histórico de vínculos do usuário.'
            : 'O perfil define quais recursos o usuário poderá acessar.'
        }
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4">
          <Campo rotulo="Nome completo" obrigatorio>
            <input className="vc-campo" value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} />
          </Campo>

          <Campo rotulo="E-mail" obrigatorio dica="Será usado como login e precisa ser único no sistema.">
            <input
              type="email"
              className="vc-campo"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
            />
          </Campo>

          {!emEdicao && (
            <>
              <Campo rotulo="Perfil de acesso" obrigatorio>
                <select
                  className="vc-campo"
                  value={form.perfil}
                  onChange={(e) => setForm({ ...form, perfil: e.target.value as Perfil })}
                >
                  {PERFIS.map((perfil) => (
                    <option key={perfil.valor} value={perfil.valor}>
                      {perfil.rotulo}
                    </option>
                  ))}
                </select>
              </Campo>

              <Campo
                rotulo="Senha inicial"
                dica="Deixe em branco para o usuário criar a própria senha pelo link enviado por e-mail (válido por 72 horas)."
              >
                <input
                  type="password"
                  className="vc-campo"
                  value={form.senha}
                  onChange={(e) => setForm({ ...form, senha: e.target.value })}
                  autoComplete="new-password"
                />
              </Campo>
            </>
          )}

          {/* Os campos abaixo dependem do perfil: o registro profissional é criado junto. */}
          {form.perfil === 'Veterinario' && (
            <div className="grid gap-4 sm:grid-cols-2">
              <Campo rotulo="CRMV" obrigatorio>
                <input className="vc-campo" value={form.crmv} onChange={(e) => setForm({ ...form, crmv: e.target.value })} />
              </Campo>

              <Campo rotulo="Especialidade">
                <input
                  className="vc-campo"
                  value={form.especialidade}
                  onChange={(e) => setForm({ ...form, especialidade: e.target.value })}
                  placeholder="Fisioterapia veterinária"
                />
              </Campo>
            </div>
          )}

          {form.perfil === 'Tutor' && (
            <div className="grid gap-4 sm:grid-cols-2">
              <Campo rotulo="Telefone">
                <input
                  className="vc-campo"
                  value={form.telefone}
                  onChange={(e) => setForm({ ...form, telefone: e.target.value })}
                />
              </Campo>

              <Campo rotulo="CPF">
                <input className="vc-campo" value={form.cpf} onChange={(e) => setForm({ ...form, cpf: e.target.value })} />
              </Campo>

              <div className="sm:col-span-2">
                <Campo rotulo="Endereço">
                  <input
                    className="vc-campo"
                    value={form.endereco}
                    onChange={(e) => setForm({ ...form, endereco: e.target.value })}
                  />
                </Campo>
              </div>
            </div>
          )}

          {form.perfil === 'Apoio' && (
            <Campo rotulo="Setor" dica={emEdicao ? 'Deixe em branco para manter o setor atual.' : undefined}>
              <input
                className="vc-campo"
                value={form.setor}
                onChange={(e) => setForm({ ...form, setor: e.target.value })}
                placeholder="Recepção"
              />
            </Campo>
          )}

          {erroForm && <Alerta tipo="erro">{erroForm}</Alerta>}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="vc-botao-secundario" onClick={() => setModalAberto(false)} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              {emEdicao ? 'Salvar alterações' : 'Cadastrar usuário'}
            </button>
          </div>
        </form>
      </Modal>

      <Modal
        aberto={!!senhaGerada}
        titulo="Senha provisória gerada"
        descricao="A senha foi enviada por e-mail ao usuário. Se preferir, repasse-a pessoalmente; ele poderá trocá-la depois de entrar."
        aoFechar={() => setSenhaGerada(null)}
      >
        {senhaGerada && (
          <div className="space-y-4">
            <div className="rounded-xl bg-slate-50 p-4">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Usuário</p>
              <p className="text-sm text-slate-800">{senhaGerada.email}</p>

              <p className="mt-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Senha provisória</p>
              <p className="font-mono text-lg font-bold text-slate-900">{senhaGerada.senha}</p>
            </div>

            <Alerta tipo="aviso">
              Esta senha é exibida uma única vez nesta tela. O usuário também a recebeu no e-mail cadastrado.
            </Alerta>

            <div className="flex justify-end">
              <button type="button" className="vc-botao-primario" onClick={() => setSenhaGerada(null)}>
                Entendi
              </button>
            </div>
          </div>
        )}
      </Modal>
    </>
  );
}
