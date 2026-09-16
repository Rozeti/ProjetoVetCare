import { useCallback, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Loader2, Mail, PawPrint, Pencil, Phone, Plus, Search, UserSquare2 } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import type { PaginaDe, Tutor } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando, Etiqueta, Modal, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { useCarregamento } from '../hooks/useCarregamento';
import { paginaVazia } from '../utils/paginacao';

const FORM_VAZIO = {
  nome: '',
  email: '',
  senha: '',
  telefone: '',
  endereco: '',
  cpf: '',
};

/** Cadastro dos tutores responsáveis pelos pacientes (apoio à HU-003 e RN-001). */
export function Tutores() {
  const { temPerfil } = useAuth();
  const podeEditar = temPerfil('Administrador', 'Veterinario', 'Apoio');

  const [numeroPagina, setNumeroPagina] = useState(1);
  const [busca, setBusca] = useState('');
  const [aviso, setAviso] = useState('');

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<Tutor | null>(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [erroForm, setErroForm] = useState('');
  const [salvando, setSalvando] = useState(false);

  const buscar = useCallback(async () => {
    const { data } = await api.get<PaginaDe<Tutor>>('/api/tutores', {
      params: { busca: busca || undefined, pagina: numeroPagina, tamanho: 20 },
    });

    return data;
  }, [busca, numeroPagina]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar os tutores.',
    300,
  );

  const pagina = dados ?? paginaVazia<Tutor>();

  // Uma busca nova invalida a página atual: o resultado pode ter menos páginas.
  function filtrar(texto: string) {
    setBusca(texto);
    setNumeroPagina(1);
  }

  function abrirNovo() {
    setEmEdicao(null);
    setForm(FORM_VAZIO);
    setErroForm('');
    setModalAberto(true);
  }

  function abrirEdicao(tutor: Tutor) {
    setEmEdicao(tutor);
    setForm({
      nome: tutor.nome,
      email: tutor.email,
      senha: '',
      telefone: tutor.telefone,
      endereco: tutor.endereco,
      cpf: tutor.cpf,
    });
    setErroForm('');
    setModalAberto(true);
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErroForm('');
    setSalvando(true);

    try {
      if (emEdicao) {
        // Nome e e-mail pertencem ao usuário e são alterados em Usuários.
        await api.put(`/api/tutores/${emEdicao.id}`, {
          telefone: form.telefone,
          endereco: form.endereco,
          cpf: form.cpf,
        });

        setAviso('Dados do tutor atualizados.');
      } else {
        if (!form.nome.trim() || !form.email.trim()) {
          setErroForm('Informe o nome e o e-mail para criar o acesso do tutor.');
          setSalvando(false);
          return;
        }

        await api.post('/api/tutores', form);
        setAviso('Tutor cadastrado com sucesso.');
      }

      setModalAberto(false);
      recarregar();
    } catch (falha) {
      setErroForm(mensagemDeErro(falha, 'Não foi possível salvar o tutor.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Tutores"
        descricao="Responsáveis legais pelos pacientes em tratamento."
        acoes={
          podeEditar && (
            <button type="button" className="vc-botao-primario" onClick={abrirNovo}>
              <Plus size={16} />
              Novo tutor
            </button>
          )
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
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
          <input
            className="vc-campo pl-10"
            placeholder="Buscar por nome, e-mail ou telefone"
            value={busca}
            onChange={(e) => filtrar(e.target.value)}
            aria-label="Buscar tutores"
          />
        </div>
      </Card>

      {carregando ? (
        <Carregando texto="Carregando tutores..." />
      ) : pagina.itens.length === 0 ? (
        <Card>
          <SemDados
            icone={<UserSquare2 size={40} />}
            titulo={busca ? 'Nenhum tutor encontrado' : 'Nenhum tutor cadastrado'}
            descricao="Cadastre o tutor antes de registrar o paciente: todo pet precisa de um responsável."
            acao={
              podeEditar && !busca ? (
                <button type="button" className="vc-botao-sutil" onClick={abrirNovo}>
                  <Plus size={16} />
                  Cadastrar tutor
                </button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {pagina.itens.map((tutor) => (
            <Card key={tutor.id} className="p-5">
              <div className="mb-3 flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="truncate font-semibold text-slate-900">{tutor.nome}</p>
                  <Etiqueta
                    className={`mt-1 ${tutor.ativo ? 'bg-sucesso-claro text-emerald-800' : 'bg-slate-200 text-slate-600'}`}
                  >
                    {tutor.ativo ? 'Ativo' : 'Inativo'}
                  </Etiqueta>
                </div>

                {podeEditar && (
                  <button
                    type="button"
                    onClick={() => abrirEdicao(tutor)}
                    className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                    title="Editar contato"
                  >
                    <Pencil size={16} />
                  </button>
                )}
              </div>

              <ul className="space-y-1.5 text-sm text-slate-600">
                <li className="flex items-center gap-2">
                  <Mail size={14} className="shrink-0 text-slate-400" />
                  <span className="truncate">{tutor.email}</span>
                </li>
                {tutor.telefone && (
                  <li className="flex items-center gap-2">
                    <Phone size={14} className="shrink-0 text-slate-400" />
                    {tutor.telefone}
                  </li>
                )}
                <li className="flex items-center gap-2">
                  <PawPrint size={14} className="shrink-0 text-slate-400" />
                  {tutor.quantidadePets} {tutor.quantidadePets === 1 ? 'paciente' : 'pacientes'}
                </li>
              </ul>

              {tutor.endereco && <p className="mt-3 text-xs text-slate-500">{tutor.endereco}</p>}

              <Link
                to={`/pacientes?tutor=${tutor.id}`}
                className="mt-4 inline-block text-sm font-medium text-brand hover:underline"
              >
                Ver pacientes
              </Link>
            </Card>
          ))}
        </div>
      )}

      {!carregando && pagina.total > 0 && (
        <Card className="mt-4">
          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="tutores" />
        </Card>
      )}

      <Modal
        aberto={modalAberto}
        titulo={emEdicao ? 'Editar tutor' : 'Novo tutor'}
        descricao={
          emEdicao
            ? 'Nome e e-mail são alterados na tela de Usuários.'
            : 'O cadastro cria também o acesso do tutor ao sistema.'
        }
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4">
          {!emEdicao && (
            <>
              <Campo rotulo="Nome completo" obrigatorio>
                <input className="vc-campo" value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} />
              </Campo>

              <Campo rotulo="E-mail" obrigatorio dica="Será o login do tutor no aplicativo e no portal web.">
                <input
                  type="email"
                  className="vc-campo"
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                />
              </Campo>

              <Campo rotulo="Senha inicial" dica="Deixe em branco para o sistema gerar uma senha provisória.">
                <input
                  type="password"
                  className="vc-campo"
                  value={form.senha}
                  onChange={(e) => setForm({ ...form, senha: e.target.value })}
                />
              </Campo>
            </>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Telefone">
              <input
                className="vc-campo"
                value={form.telefone}
                onChange={(e) => setForm({ ...form, telefone: e.target.value })}
                placeholder="(61) 99999-0000"
              />
            </Campo>

            <Campo rotulo="CPF">
              <input className="vc-campo" value={form.cpf} onChange={(e) => setForm({ ...form, cpf: e.target.value })} />
            </Campo>
          </div>

          <Campo rotulo="Endereço">
            <input
              className="vc-campo"
              value={form.endereco}
              onChange={(e) => setForm({ ...form, endereco: e.target.value })}
            />
          </Campo>

          {erroForm && <Alerta tipo="erro">{erroForm}</Alerta>}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="vc-botao-secundario" onClick={() => setModalAberto(false)} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              {emEdicao ? 'Salvar alterações' : 'Cadastrar tutor'}
            </button>
          </div>
        </form>
      </Modal>
    </>
  );
}
