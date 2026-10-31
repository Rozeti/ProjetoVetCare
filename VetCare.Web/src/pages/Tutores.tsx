import { useCallback, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Loader2, PawPrint, Pencil, Plus, Search, UserSquare2 } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import {
  compactar,
  DICA_SENHA,
  mascararCpf,
  mascararTelefone,
  normalizarCpf,
  normalizarTelefone,
  validarEmail,
  validarEndereco,
  validarNome,
  validarSenha,
} from '../utils/formato';
import { useAuth } from '../contexts/auth';
import type { PaginaDe, Tutor } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando, Etiqueta, Modal, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { useConfirmacao } from '../hooks/useConfirmacao';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { paginaVazia } from '../utils/paginacao';

const FORM_VAZIO = {
  nome: '',
  email: '',
  senha: '',
  telefone: '',
  endereco: '',
  cpf: '',
};

type CampoDoForm = keyof typeof FORM_VAZIO;

/**
 * Cadastro dos tutores responsáveis pelos pacientes (apoio à HU-003 e RN-001). Toda a equipe
 * clínica, inclusive qualquer veterinário, vê todos os tutores, com os animais de cada um.
 */
export function Tutores() {
  const { temPerfil } = useAuth();
  const { confirmarEdicao } = useConfirmacao();
  // RN-014: o veterinário consulta a lista; a ficha é mantida pela administração.
  const podeEditar = temPerfil('Administrador', 'Apoio');

  const [numeroPagina, setNumeroPagina] = useState(1);
  const [busca, setBusca] = useState('');
  const [aviso, setAviso] = useState('');

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<Tutor | null>(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [erros, setErros] = useState<Partial<Record<CampoDoForm, string>>>({});
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

  useAtualizacao(['tutores', 'usuarios', 'pets'], recarregar);

  const pagina = dados ?? paginaVazia<Tutor>();

  // Uma busca nova invalida a página atual: o resultado pode ter menos páginas.
  function filtrar(texto: string) {
    setBusca(texto);
    setNumeroPagina(1);
  }

  function abrirNovo() {
    setEmEdicao(null);
    setForm(FORM_VAZIO);
    setErros({});
    setErroForm('');
    setModalAberto(true);
  }

  function abrirEdicao(tutor: Tutor) {
    setEmEdicao(tutor);
    setForm({
      nome: tutor.nome,
      email: tutor.email,
      senha: '',
      telefone: mascararTelefone(tutor.telefone),
      endereco: tutor.endereco,
      cpf: mascararCpf(tutor.cpf),
    });
    setErros({});
    setErroForm('');
    setModalAberto(true);
  }

  function atualizar(campo: CampoDoForm, valor: string) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
    // O erro do campo some assim que ele é editado.
    setErros((atuais) => (atuais[campo] ? { ...atuais, [campo]: undefined } : atuais));
  }

  /**
   * As mesmas regras da API (ValidacaoDeCadastro), mostradas ao lado de cada campo. O telefone
   * é obrigatório (é por ele que a clínica fala com o tutor); CPF e endereço são opcionais,
   * mas, quando preenchidos, precisam estar corretos. Devolve telefone e CPF padronizados.
   */
  function validar(): { telefone: string; cpf: string } | null {
    const encontrados: Partial<Record<CampoDoForm, string>> = {};

    // Um cadastro antigo continua editável sem trocar de nome; o nome novo segue a regra.
    if (!emEdicao || compactar(form.nome) !== emEdicao.nome) {
      const erroDeNome = validarNome(form.nome);
      if (erroDeNome) encontrados.nome = erroDeNome;
    }

    const erroDeEmail = validarEmail(form.email);
    if (erroDeEmail) encontrados.email = erroDeEmail;

    if (!emEdicao && form.senha) {
      const erroDeSenha = validarSenha(form.senha);
      if (erroDeSenha) encontrados.senha = `${erroDeSenha} Ou deixe em branco para o tutor criar a própria senha.`;
    }

    const telefone = normalizarTelefone(form.telefone, true, 'o telefone do tutor');
    if (telefone.erro) encontrados.telefone = telefone.erro;

    const cpf = normalizarCpf(form.cpf);
    if (cpf.erro) encontrados.cpf = cpf.erro;

    const erroDeEndereco = validarEndereco(form.endereco);
    if (erroDeEndereco) encontrados.endereco = erroDeEndereco;

    setErros(encontrados);

    if (Object.keys(encontrados).length > 0) {
      setErroForm('Confira os campos destacados.');
      return null;
    }

    return { telefone: telefone.valor ?? '', cpf: cpf.valor ?? '' };
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErroForm('');

    const valido = validar();

    if (!valido) {
      return;
    }

    if (
      emEdicao &&
      !(await confirmarEdicao(
        <>
          Salvar as alterações no cadastro de <strong>{emEdicao.nome}</strong>?
        </>,
      ))
    ) {
      return;
    }

    setSalvando(true);

    const dados = {
      nome: compactar(form.nome),
      email: form.email.trim(),
      telefone: valido.telefone,
      cpf: valido.cpf,
      endereco: compactar(form.endereco),
    };

    try {
      if (emEdicao) {
        await api.put(`/api/tutores/${emEdicao.id}`, dados);
        setAviso('Dados do tutor atualizados.');
      } else {
        await api.post('/api/tutores', { ...dados, senha: form.senha || null });
        setAviso(
          form.senha
            ? 'Tutor cadastrado com sucesso. Ele recebeu um e-mail de boas-vindas.'
            : 'Tutor cadastrado. Ele recebeu por e-mail o código para criar a própria senha.',
        );
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
        descricao="Responsáveis legais pelos pacientes da clínica, com os animais de cada um."
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
        <Alerta flutuante tipo="erro" aoFechar={() => setErro('')}>
          {erro}
        </Alerta>
      )}
      {aviso && (
        <Alerta flutuante tipo="sucesso" aoFechar={() => setAviso('')}>
          {aviso}
        </Alerta>
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
        // A mesma apresentação da lista de pacientes: tabela com a paginação no rodapé.
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th scope="col">Tutor</th>
                  <th scope="col">Telefone</th>
                  <th scope="col">Endereço</th>
                  <th scope="col">Animais vinculados</th>
                  <th scope="col">Status</th>
                  <th scope="col" className="text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((tutor) => (
                  <tr key={tutor.id}>
                    <td>
                      <span className="block font-semibold text-slate-900">{tutor.nome}</span>
                      <span className="text-xs text-slate-400">{tutor.email}</span>
                    </td>
                    <td>{tutor.telefone || <span className="text-slate-400">—</span>}</td>
                    <td className="max-w-xs">
                      {tutor.endereco ? (
                        <span className="line-clamp-2">{tutor.endereco}</span>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                    <td className="max-w-sm">
                      <AnimaisDoTutor tutor={tutor} />
                    </td>
                    <td>
                      <Etiqueta className={tutor.ativo ? 'bg-sucesso-claro text-emerald-800' : 'bg-slate-200 text-slate-600'}>
                        {tutor.ativo ? 'Ativo' : 'Inativo'}
                      </Etiqueta>
                    </td>
                    <td>
                      <div className="flex items-center justify-end gap-1">
                        <Link
                          to={`/pacientes?tutor=${tutor.id}`}
                          className="rounded-lg p-2 text-slate-500 hover:bg-brand-100 hover:text-brand"
                          title="Ver pacientes do tutor"
                        >
                          <PawPrint size={16} />
                        </Link>

                        {podeEditar && (
                          <button
                            type="button"
                            onClick={() => abrirEdicao(tutor)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                            title="Editar tutor"
                            aria-label={`Editar ${tutor.nome}`}
                          >
                            <Pencil size={16} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="tutores" />
        </Card>
      )}

      <Modal
        aberto={modalAberto}
        titulo={emEdicao ? 'Editar tutor' : 'Novo tutor'}
        descricao={
          emEdicao
            ? 'Mudar o e-mail muda também o login do tutor no portal e no aplicativo.'
            : 'O cadastro cria também o acesso do tutor ao sistema.'
        }
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4" noValidate>
          <Campo rotulo="Nome completo" obrigatorio erro={erros.nome} dica="Nome e sobrenome, só com letras.">
            <input
              className="vc-campo"
              value={form.nome}
              onChange={(e) => atualizar('nome', e.target.value)}
              maxLength={120}
              autoComplete="name"
            />
          </Campo>

          <Campo
            rotulo="E-mail"
            obrigatorio
            erro={erros.email}
            dica="Será o login do tutor no aplicativo e no portal web."
          >
            <input
              type="email"
              className="vc-campo"
              value={form.email}
              onChange={(e) => atualizar('email', e.target.value)}
              maxLength={180}
              autoComplete="email"
            />
          </Campo>

          {!emEdicao && (
            <Campo
              rotulo="Senha inicial"
              erro={erros.senha}
              dica={`${DICA_SENHA} Deixe em branco para o tutor criar a própria senha com o código enviado por e-mail (válido por 72 horas).`}
            >
              <input
                type="password"
                className="vc-campo"
                value={form.senha}
                onChange={(e) => atualizar('senha', e.target.value)}
                autoComplete="new-password"
                maxLength={64}
              />
            </Campo>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Telefone" obrigatorio erro={erros.telefone} dica="Com DDD. É por ele que a clínica fala com o tutor.">
              <input
                className="vc-campo"
                inputMode="tel"
                value={form.telefone}
                onChange={(e) => atualizar('telefone', mascararTelefone(e.target.value))}
                placeholder="(61) 99999-0000"
                autoComplete="tel"
              />
            </Campo>

            <Campo rotulo="CPF" erro={erros.cpf} dica="Opcional. Não pode se repetir entre tutores.">
              <input
                className="vc-campo"
                inputMode="numeric"
                value={form.cpf}
                onChange={(e) => atualizar('cpf', mascararCpf(e.target.value))}
                placeholder="000.000.000-00"
              />
            </Campo>
          </div>

          <Campo rotulo="Endereço" erro={erros.endereco} dica="Opcional. Rua, número e bairro.">
            <input
              className="vc-campo"
              value={form.endereco}
              onChange={(e) => atualizar('endereco', e.target.value)}
              maxLength={250}
              autoComplete="street-address"
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

/** Quantos animais aparecem por extenso antes do "+N", para a linha não crescer demais. */
const ANIMAIS_VISIVEIS = 4;

/** Animais vinculados ao tutor, cada um levando ao próprio prontuário. */
function AnimaisDoTutor({ tutor }: { tutor: Tutor }) {
  if (tutor.pets.length === 0) {
    return <span className="text-sm text-slate-400">Nenhum animal vinculado</span>;
  }

  const visiveis = tutor.pets.slice(0, ANIMAIS_VISIVEIS);
  const restantes = tutor.pets.length - visiveis.length;

  return (
    <ul className="flex flex-wrap gap-1.5" aria-label={`Animais de ${tutor.nome}`}>
      {visiveis.map((pet) => (
        <li key={pet.id}>
          <Link
            to={`/prontuario/${pet.id}`}
            className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium transition ${
              pet.ativo
                ? 'bg-brand-50 text-brand-dark hover:bg-brand-100'
                : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
            }`}
            title={`${pet.especie}${pet.ativo ? '' : ' · inativo'} — abrir prontuário`}
          >
            <PawPrint size={12} />
            {pet.nome}
            {!pet.ativo && <span className="text-[10px] uppercase">inativo</span>}
          </Link>
        </li>
      ))}
      {restantes > 0 && (
        <li>
          <Link
            to={`/pacientes?tutor=${tutor.id}`}
            className="inline-flex rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-600 hover:bg-slate-200"
          >
            +{restantes}
          </Link>
        </li>
      )}
    </ul>
  );
}
