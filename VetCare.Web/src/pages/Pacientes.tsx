import { useCallback, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import {
  ArrowRightLeft,
  FileText,
  Loader2,
  PawPrint,
  Pencil,
  Plus,
  Power,
  Search,
  Syringe,
  Trash2,
  X,
} from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import { ESPECIES, type PaginaDe, type Pet, type Tutor, type Veterinario } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando, Etiqueta, Modal, SemDados } from '../components/ui';
import { AlertasClinicos } from '../components/AlertasClinicos';
import { Paginacao } from '../components/Paginacao';
import { formatarData, formatarPeso, paraValorInputData } from '../utils/formato';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { paginaVazia } from '../utils/paginacao';
import { ModalTransferirPaciente, type PacienteParaTransferir } from './componentes/ModalTransferirPaciente';

const FORM_VAZIO = {
  nome: '',
  especie: 'Cachorro',
  raca: '',
  sexo: 'Macho',
  pelagem: '',
  microchip: '',
  castrado: false,
  dataNascimento: '',
  pesoAtualKg: '',
  tutorId: '',
  veterinarioResponsavelId: '',
  dataObito: '',
};

/** Valor do filtro que mostra só os pacientes ainda sem veterinário designado. */
const SEM_RESPONSAVEL = 'sem-responsavel';

/**
 * HU-003: gestão dos pacientes (pets) vinculados a um tutor responsável. Cada paciente
 * é acompanhado por um único veterinário: o profissional vê só os seus, enquanto a
 * administração e o apoio veem a clínica inteira e podem transferir pacientes.
 */
export function Pacientes() {
  const { usuario, temPerfil, ehVeterinario } = useAuth();
  const podeEditar = temPerfil('Administrador', 'Veterinario', 'Apoio');
  const podeInativar = temPerfil('Administrador', 'Veterinario');
  const podeExcluir = temPerfil('Administrador');
  // Quem vê a clínica inteira escolhe o responsável; o veterinário assume os próprios cadastros.
  const administraResponsaveis = temPerfil('Administrador', 'Apoio');

  // O atalho "Ver pacientes" da tela de tutores chega com ?tutor=<id>.
  const [parametros, setParametros] = useSearchParams();
  const tutorFiltrado = parametros.get('tutor') ?? '';

  const [numeroPagina, setNumeroPagina] = useState(1);
  const [busca, setBusca] = useState('');
  const [veterinarioFiltrado, setVeterinarioFiltrado] = useState('');
  const [mostrarInativos, setMostrarInativos] = useState(false);
  const [aviso, setAviso] = useState('');

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<Pet | null>(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [erroForm, setErroForm] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [emTransferencia, setEmTransferencia] = useState<PacienteParaTransferir | null>(null);

  const buscar = useCallback(async () => {
    const [respostaPets, respostaTutores, respostaVeterinarios] = await Promise.all([
      api.get<PaginaDe<Pet>>('/api/pets', {
        params: {
          busca: busca || undefined,
          tutorId: tutorFiltrado || undefined,
          veterinarioId:
            veterinarioFiltrado && veterinarioFiltrado !== SEM_RESPONSAVEL ? veterinarioFiltrado : undefined,
          semResponsavel: veterinarioFiltrado === SEM_RESPONSAVEL ? true : undefined,
          ativo: mostrarInativos ? undefined : true,
          pagina: numeroPagina,
          tamanho: 20,
        },
      }),
      api.get<Tutor[]>('/api/tutores/selecao'),
      // O veterinário não escolhe responsável nem filtra por colega: a lista só serve à administração.
      administraResponsaveis ? api.get<Veterinario[]>('/api/veterinarios') : Promise.resolve(null),
    ]);

    return {
      pagina: respostaPets.data,
      tutores: respostaTutores.data,
      veterinarios: respostaVeterinarios?.data ?? [],
    };
  }, [busca, tutorFiltrado, veterinarioFiltrado, mostrarInativos, numeroPagina, administraResponsaveis]);

  // O atraso evita disparar uma requisição por tecla digitada na busca.
  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar os pacientes.',
    300,
  );

  // Um pet cadastrado pelo tutor pelo aplicativo aparece aqui assim que ele salva.
  useAtualizacao(['pets', 'tutores', 'alergias', 'veterinarios'], recarregar);

  const pagina = dados?.pagina ?? paginaVazia<Pet>();
  const tutores = dados?.tutores ?? [];
  const veterinarios = dados?.veterinarios ?? [];
  const veterinariosAtivos = veterinarios.filter((v) => v.ativo);
  const nomeDoTutorFiltrado = tutores.find((t) => t.id === tutorFiltrado)?.nome;

  // Um filtro novo invalida a página atual: o resultado pode ter menos páginas.
  function filtrar(aplicar: () => void) {
    aplicar();
    setNumeroPagina(1);
  }

  function limparFiltroDeTutor() {
    parametros.delete('tutor');
    setParametros(parametros, { replace: true });
    setNumeroPagina(1);
  }

  function podeTransferir(pet: Pet) {
    if (administraResponsaveis) return true;
    // O veterinário só passa adiante um paciente que está com ele.
    return ehVeterinario && !!pet.veterinarioResponsavelId && pet.veterinarioResponsavelId === usuario?.veterinarioId;
  }

  function abrirNovo() {
    setEmEdicao(null);
    setForm({
      ...FORM_VAZIO,
      tutorId: tutorFiltrado,
      veterinarioResponsavelId: veterinarioFiltrado !== SEM_RESPONSAVEL ? veterinarioFiltrado : '',
    });
    setErroForm('');
    setModalAberto(true);
  }

  function abrirEdicao(pet: Pet) {
    setEmEdicao(pet);
    setForm({
      nome: pet.nome,
      especie: pet.especie,
      raca: pet.raca,
      sexo: pet.sexo || 'Macho',
      pelagem: pet.pelagem,
      microchip: pet.microchip,
      castrado: pet.castrado,
      dataNascimento: paraValorInputData(pet.dataNascimento),
      pesoAtualKg: pet.pesoAtualKg?.toString() ?? '',
      tutorId: pet.tutorId,
      veterinarioResponsavelId: pet.veterinarioResponsavelId ?? '',
      dataObito: pet.dataObito ? paraValorInputData(pet.dataObito) : '',
    });
    setErroForm('');
    setModalAberto(true);
  }

  function abrirTransferencia(pet: Pet) {
    setModalAberto(false);
    setEmTransferencia({
      id: pet.id,
      nome: pet.nome,
      veterinarioResponsavelId: pet.veterinarioResponsavelId,
      nomeVeterinarioResponsavel: pet.nomeVeterinarioResponsavel,
    });
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErroForm('');

    // HU-003, CA-2 / RN-001: o tutor é obrigatório e sinalizado antes do envio.
    if (!form.tutorId) {
      setErroForm('Selecione o tutor responsável pelo paciente.');
      return;
    }

    if (!emEdicao && administraResponsaveis && !form.veterinarioResponsavelId) {
      setErroForm('Selecione o veterinário que vai acompanhar o paciente.');
      return;
    }

    if (!form.nome.trim() || !form.dataNascimento) {
      setErroForm('Informe o nome e a data de nascimento do paciente.');
      return;
    }

    if (form.dataObito && form.dataObito < form.dataNascimento) {
      setErroForm('A data de óbito não pode ser anterior à data de nascimento.');
      return;
    }

    setSalvando(true);

    const corpo = {
      nome: form.nome,
      especie: form.especie,
      raca: form.raca,
      sexo: form.sexo,
      pelagem: form.pelagem,
      microchip: form.microchip,
      castrado: form.castrado,
      dataNascimento: form.dataNascimento,
      pesoAtualKg: form.pesoAtualKg ? Number(form.pesoAtualKg) : null,
      tutorId: form.tutorId,
    };

    try {
      if (emEdicao) {
        // A edição envia a data de óbito junto: omiti-la apagaria o registro existente.
        // O veterinário responsável não vai aqui: ele muda só pela transferência.
        await api.put(`/api/pets/${emEdicao.id}`, { ...corpo, dataObito: form.dataObito || null });
        setAviso('Paciente atualizado com sucesso.');
      } else {
        await api.post('/api/pets', {
          ...corpo,
          veterinarioResponsavelId: form.veterinarioResponsavelId || null,
        });
        setAviso('Paciente cadastrado com sucesso.');
      }

      setModalAberto(false);
      recarregar();
    } catch (falha) {
      setErroForm(mensagemDeErro(falha, 'Não foi possível salvar o paciente.'));
    } finally {
      setSalvando(false);
    }
  }

  /** HU-003, CA-4: pacientes com prontuário são inativados, nunca excluídos. */
  async function alternarStatus(pet: Pet) {
    setErro('');

    try {
      await api.patch(`/api/pets/${pet.id}/status`, { ativo: !pet.ativo });
      setAviso(pet.ativo ? `${pet.nome} foi inativado.` : `${pet.nome} foi reativado.`);
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível alterar o status do paciente.'));
    }
  }

  /** HU-003, CA-4: só um cadastro sem histórico clínico pode ser excluído de fato. */
  async function excluir(pet: Pet) {
    if (!window.confirm(`Excluir o cadastro de ${pet.nome}? Esta ação só é permitida sem registros clínicos e não pode ser desfeita.`)) {
      return;
    }

    setErro('');

    try {
      await api.delete(`/api/pets/${pet.id}`);
      setAviso(`${pet.nome} foi excluído.`);
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível excluir o paciente.'));
    }
  }

  const filtrosAtivos = !!busca || !!tutorFiltrado || !!veterinarioFiltrado;

  return (
    <>
      <CabecalhoPagina
        titulo={ehVeterinario ? 'Meus pacientes' : 'Pacientes'}
        descricao={
          ehVeterinario
            ? 'Pacientes sob sua responsabilidade. Um paciente transferido para outro veterinário sai desta lista.'
            : 'Cadastro dos pets em tratamento, com o tutor e o veterinário responsável por cada um.'
        }
        acoes={
          podeEditar && (
            <button type="button" className="vc-botao-primario" onClick={abrirNovo}>
              <Plus size={16} />
              Novo paciente
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
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-64 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
            <input
              className="vc-campo pl-10"
              placeholder="Buscar por nome, espécie, raça ou tutor"
              value={busca}
              onChange={(e) => filtrar(() => setBusca(e.target.value))}
              aria-label="Buscar pacientes"
            />
          </div>

          {administraResponsaveis && (
            <select
              className="vc-campo w-auto min-w-56"
              value={veterinarioFiltrado}
              onChange={(e) => filtrar(() => setVeterinarioFiltrado(e.target.value))}
              aria-label="Filtrar por veterinário responsável"
            >
              <option value="">Todos os veterinários</option>
              <option value={SEM_RESPONSAVEL}>Sem veterinário responsável</option>
              {veterinarios.map((vet) => (
                <option key={vet.id} value={vet.id}>
                  {vet.nome}
                </option>
              ))}
            </select>
          )}

          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input
              type="checkbox"
              className="h-4 w-4 rounded accent-brand"
              checked={mostrarInativos}
              onChange={(e) => filtrar(() => setMostrarInativos(e.target.checked))}
            />
            Mostrar inativos
          </label>

          {tutorFiltrado && (
            <button
              type="button"
              onClick={limparFiltroDeTutor}
              className="inline-flex items-center gap-1.5 rounded-full bg-brand-100 px-3 py-1 text-xs font-semibold text-brand-dark hover:bg-brand-200"
              title="Remover o filtro por tutor"
            >
              Tutor: {nomeDoTutorFiltrado ?? 'selecionado'}
              <X size={14} />
            </button>
          )}
        </div>
      </Card>

      {carregando ? (
        <Carregando texto="Carregando pacientes..." />
      ) : pagina.itens.length === 0 ? (
        <Card>
          <SemDados
            icone={<PawPrint size={40} />}
            titulo={filtrosAtivos ? 'Nenhum paciente encontrado' : 'Nenhum paciente cadastrado'}
            descricao={
              filtrosAtivos
                ? 'Tente outro termo de busca ou limpe o filtro.'
                : ehVeterinario
                  ? 'Os pacientes que você cadastrar, ou que forem transferidos para você, aparecem aqui.'
                  : 'Cadastre o primeiro paciente para começar a registrar a evolução clínica.'
            }
            acao={
              podeEditar && !busca ? (
                <button type="button" className="vc-botao-sutil" onClick={abrirNovo}>
                  <Plus size={16} />
                  Cadastrar paciente
                </button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th>Paciente</th>
                  <th>Espécie / Raça</th>
                  <th>Idade</th>
                  <th>Peso</th>
                  <th>Tutor</th>
                  {!ehVeterinario && <th>Veterinário</th>}
                  <th>Status</th>
                  <th className="text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((pet) => (
                  <tr key={pet.id}>
                    <td>
                      <Link to={`/prontuario/${pet.id}`} className="font-semibold text-slate-900 hover:text-brand">
                        {pet.nome}
                      </Link>
                      {pet.microchip && (
                        <span className="block text-xs text-slate-400">Chip {pet.microchip}</span>
                      )}
                      {pet.alertasClinicos.length > 0 && (
                        <div className="mt-1">
                          <AlertasClinicos alertas={pet.alertasClinicos} compacto />
                        </div>
                      )}
                    </td>
                    <td>
                      {pet.especie}
                      {pet.raca && <span className="text-slate-400"> · {pet.raca}</span>}
                    </td>
                    <td>{pet.idadeDescritiva}</td>
                    <td>{formatarPeso(pet.pesoAtualKg)}</td>
                    <td>
                      <span className="block">{pet.nomeTutor}</span>
                      {pet.telefoneTutor && <span className="text-xs text-slate-400">{pet.telefoneTutor}</span>}
                    </td>
                    {!ehVeterinario && (
                      <td>
                        {pet.nomeVeterinarioResponsavel ? (
                          pet.nomeVeterinarioResponsavel
                        ) : (
                          <Etiqueta className="bg-alerta-claro text-amber-800">Sem responsável</Etiqueta>
                        )}
                      </td>
                    )}
                    <td>
                      {pet.dataObito ? (
                        <span title={`Óbito em ${formatarData(pet.dataObito)}`}>
                          <Etiqueta className="bg-slate-200 text-slate-600">Falecido</Etiqueta>
                        </span>
                      ) : (
                        <Etiqueta className={pet.ativo ? 'bg-sucesso-claro text-emerald-800' : 'bg-slate-200 text-slate-600'}>
                          {pet.ativo ? 'Ativo' : 'Inativo'}
                        </Etiqueta>
                      )}
                    </td>
                    <td>
                      <div className="flex items-center justify-end gap-1">
                        <Link
                          to={`/prontuario/${pet.id}`}
                          className="rounded-lg p-2 text-slate-500 hover:bg-brand-100 hover:text-brand"
                          title="Abrir prontuário"
                        >
                          <FileText size={16} />
                        </Link>

                        <Link
                          to={`/prontuario/${pet.id}?aba=vacinas`}
                          className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                          title="Carteira de vacinação"
                        >
                          <Syringe size={16} />
                        </Link>

                        {podeEditar && (
                          <button
                            type="button"
                            onClick={() => abrirEdicao(pet)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                            title="Editar cadastro"
                          >
                            <Pencil size={16} />
                          </button>
                        )}

                        {podeTransferir(pet) && !pet.dataObito && (
                          <button
                            type="button"
                            onClick={() => abrirTransferencia(pet)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-brand-100 hover:text-brand"
                            title={
                              pet.veterinarioResponsavelId
                                ? 'Transferir para outro veterinário'
                                : 'Definir veterinário responsável'
                            }
                          >
                            <ArrowRightLeft size={16} />
                          </button>
                        )}

                        {podeInativar && !pet.dataObito && (
                          <button
                            type="button"
                            onClick={() => alternarStatus(pet)}
                            className={`rounded-lg p-2 ${
                              pet.ativo ? 'text-slate-500 hover:bg-perigo-claro hover:text-perigo' : 'text-sucesso hover:bg-sucesso-claro'
                            }`}
                            title={pet.ativo ? 'Inativar paciente' : 'Reativar paciente'}
                          >
                            <Power size={16} />
                          </button>
                        )}

                        {podeExcluir && (
                          <button
                            type="button"
                            onClick={() => excluir(pet)}
                            className="rounded-lg p-2 text-slate-400 hover:bg-perigo-claro hover:text-perigo"
                            title="Excluir cadastro (apenas sem histórico clínico)"
                          >
                            <Trash2 size={16} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="pacientes" />
        </Card>
      )}

      <Modal
        aberto={modalAberto}
        titulo={emEdicao ? 'Editar paciente' : 'Novo paciente'}
        descricao={
          emEdicao
            ? 'A alteração preserva todo o histórico clínico já registrado.'
            : ehVeterinario
              ? 'O paciente fica sob sua responsabilidade e precisa estar vinculado a um tutor.'
              : 'O paciente precisa estar vinculado a um tutor e a um veterinário responsável.'
        }
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4">
          <Campo rotulo="Nome do paciente" obrigatorio>
            <input
              className="vc-campo"
              value={form.nome}
              onChange={(e) => setForm({ ...form, nome: e.target.value })}
              placeholder="Ex.: Thor"
            />
          </Campo>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Espécie" obrigatorio>
              <select className="vc-campo" value={form.especie} onChange={(e) => setForm({ ...form, especie: e.target.value })}>
                {ESPECIES.map((especie) => (
                  <option key={especie}>{especie}</option>
                ))}
              </select>
            </Campo>

            <Campo rotulo="Raça">
              <input className="vc-campo" value={form.raca} onChange={(e) => setForm({ ...form, raca: e.target.value })} />
            </Campo>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Pelagem">
              <input
                className="vc-campo"
                value={form.pelagem}
                onChange={(e) => setForm({ ...form, pelagem: e.target.value })}
                placeholder="Ex.: caramelo curto"
              />
            </Campo>

            <Campo rotulo="Microchip" dica="Não pode se repetir na clínica.">
              <input
                className="vc-campo"
                value={form.microchip}
                onChange={(e) => setForm({ ...form, microchip: e.target.value })}
              />
            </Campo>
          </div>

          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              className="h-4 w-4 rounded accent-brand"
              checked={form.castrado}
              onChange={(e) => setForm({ ...form, castrado: e.target.checked })}
            />
            Paciente castrado
          </label>

          <div className="grid gap-4 sm:grid-cols-3">
            <Campo rotulo="Sexo">
              <select className="vc-campo" value={form.sexo} onChange={(e) => setForm({ ...form, sexo: e.target.value })}>
                <option>Macho</option>
                <option>Fêmea</option>
              </select>
            </Campo>

            <Campo rotulo="Nascimento" obrigatorio>
              <input
                type="date"
                className="vc-campo"
                max={paraValorInputData(new Date())}
                value={form.dataNascimento}
                onChange={(e) => setForm({ ...form, dataNascimento: e.target.value })}
              />
            </Campo>

            <Campo rotulo="Peso (kg)">
              <input
                type="number"
                step="0.1"
                min="0.1"
                className="vc-campo"
                value={form.pesoAtualKg}
                onChange={(e) => setForm({ ...form, pesoAtualKg: e.target.value })}
              />
            </Campo>
          </div>

          <Campo rotulo="Tutor responsável" obrigatorio dica="Cada paciente pertence a um único tutor.">
            <select className="vc-campo" value={form.tutorId} onChange={(e) => setForm({ ...form, tutorId: e.target.value })}>
              <option value="">Selecione o tutor</option>
              {tutores.map((tutor) => (
                <option key={tutor.id} value={tutor.id}>
                  {tutor.nome} {tutor.telefone && `— ${tutor.telefone}`}
                </option>
              ))}
            </select>
          </Campo>

          {/* No cadastro a administração escolhe o responsável; o veterinário assume sozinho. */}
          {!emEdicao && administraResponsaveis && (
            <Campo
              rotulo="Veterinário responsável"
              obrigatorio
              dica="Só este profissional (além da administração) verá o paciente. Pode ser trocado depois pela transferência."
            >
              <select
                className="vc-campo"
                value={form.veterinarioResponsavelId}
                onChange={(e) => setForm({ ...form, veterinarioResponsavelId: e.target.value })}
              >
                <option value="">Selecione o veterinário</option>
                {veterinariosAtivos.map((vet) => (
                  <option key={vet.id} value={vet.id}>
                    {vet.nome}
                    {vet.especialidade && ` — ${vet.especialidade}`}
                  </option>
                ))}
              </select>
            </Campo>
          )}

          {/* Na edição o responsável é só leitura: a troca passa pela transferência, que leva a agenda junto. */}
          {emEdicao && !ehVeterinario && (
            <Campo rotulo="Veterinário responsável" dica="A troca é feita pela transferência, que leva junto os tratamentos e as sessões.">
              <div className="flex items-center gap-2">
                <input
                  className="vc-campo"
                  value={emEdicao.nomeVeterinarioResponsavel || 'Sem veterinário responsável'}
                  readOnly
                />
                {podeTransferir(emEdicao) && !emEdicao.dataObito && (
                  <button type="button" className="vc-botao-secundario shrink-0" onClick={() => abrirTransferencia(emEdicao)}>
                    <ArrowRightLeft size={16} />
                    {emEdicao.veterinarioResponsavelId ? 'Transferir' : 'Definir'}
                  </button>
                )}
              </div>
            </Campo>
          )}

          {emEdicao && (
            <Campo
              rotulo="Data de óbito"
              dica="Registrar o óbito inativa o paciente e encerra os tratamentos em aberto. Deixe em branco se não se aplica."
            >
              <input
                type="date"
                className="vc-campo"
                min={form.dataNascimento || undefined}
                max={paraValorInputData(new Date())}
                value={form.dataObito}
                onChange={(e) => setForm({ ...form, dataObito: e.target.value })}
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
              {emEdicao ? 'Salvar alterações' : 'Cadastrar paciente'}
            </button>
          </div>
        </form>
      </Modal>

      <ModalTransferirPaciente
        paciente={emTransferencia}
        aoFechar={() => setEmTransferencia(null)}
        aoTransferir={(mensagem) => {
          setEmTransferencia(null);
          setAviso(mensagem);
          recarregar();
        }}
      />
    </>
  );
}
