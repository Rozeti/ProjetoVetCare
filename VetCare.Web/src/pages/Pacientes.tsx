import { useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Eye, PawPrint, Plus, Power, Search, X } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import type { PaginaDe, Pet, Tutor, Veterinario } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { AlertasClinicos } from '../components/AlertasClinicos';
import { Paginacao } from '../components/Paginacao';
import { formatarDia, formatarPeso } from '../utils/formato';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { paginaVazia } from '../utils/paginacao';
import { useConfirmacao } from '../hooks/useConfirmacao';
import { ModalCadastroPaciente } from './componentes/ModalCadastroPaciente';

/** Valor do filtro que mostra só os pacientes ainda sem veterinário designado. */
const SEM_RESPONSAVEL = 'sem-responsavel';

/**
 * HU-003: lista dos pacientes (pets) vinculados a um tutor responsável. Toda a equipe clínica,
 * inclusive qualquer veterinário, vê todos os pacientes da clínica: se o responsável faltar, um
 * colega atende o animal. O veterinário filtra pelos próprios pacientes quando quiser. A lista
 * só visualiza e inativa: editar o cadastro, vacinar e registrar a parte clínica ficam no
 * prontuário do animal.
 */
export function Pacientes() {
  const { temPerfil, ehVeterinario, usuario } = useAuth();
  const podeCadastrar = temPerfil('Administrador', 'Veterinario', 'Apoio');
  const podeInativar = temPerfil('Administrador', 'Veterinario');
  const meuVeterinarioId = ehVeterinario ? usuario?.veterinarioId ?? '' : '';

  // O atalho "Ver pacientes" da tela de tutores chega com ?tutor=<id>.
  const [parametros, setParametros] = useSearchParams();
  const tutorFiltrado = parametros.get('tutor') ?? '';

  const [numeroPagina, setNumeroPagina] = useState(1);
  const [busca, setBusca] = useState('');
  const [racaFiltrada, setRacaFiltrada] = useState('');
  const [veterinarioFiltrado, setVeterinarioFiltrado] = useState('');
  const [mostrarInativos, setMostrarInativos] = useState(false);
  const [aviso, setAviso] = useState('');
  const [cadastroAberto, setCadastroAberto] = useState(false);
  const { confirmar } = useConfirmacao();

  const buscar = useCallback(async () => {
    const [respostaPets, respostaTutores, respostaRacas, respostaVeterinarios] = await Promise.all([
      api.get<PaginaDe<Pet>>('/api/pets', {
        params: {
          busca: busca || undefined,
          tutorId: tutorFiltrado || undefined,
          raca: racaFiltrada || undefined,
          veterinarioId:
            veterinarioFiltrado && veterinarioFiltrado !== SEM_RESPONSAVEL ? veterinarioFiltrado : undefined,
          semResponsavel: veterinarioFiltrado === SEM_RESPONSAVEL ? true : undefined,
          ativo: mostrarInativos ? undefined : true,
          pagina: numeroPagina,
          tamanho: 20,
        },
      }),
      // Só o nome do tutor filtrado é usado, para a etiqueta do filtro.
      tutorFiltrado ? api.get<Tutor[]>('/api/tutores/selecao') : Promise.resolve(null),
      api.get<string[]>('/api/pets/racas'),
      api.get<Veterinario[]>('/api/veterinarios'),
    ]);

    return {
      pagina: respostaPets.data,
      tutores: respostaTutores?.data ?? [],
      racas: respostaRacas.data,
      veterinarios: respostaVeterinarios?.data ?? [],
    };
  }, [busca, tutorFiltrado, racaFiltrada, veterinarioFiltrado, mostrarInativos, numeroPagina]);

  // O atraso evita disparar uma requisição por tecla digitada na busca.
  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar os pacientes.',
    300,
  );

  // Um pet cadastrado pelo tutor pelo aplicativo aparece aqui assim que ele salva.
  useAtualizacao(['pets', 'tutores', 'alergias', 'veterinarios', 'usuarios', 'vacinas'], recarregar);

  const pagina = dados?.pagina ?? paginaVazia<Pet>();
  const veterinarios = dados?.veterinarios ?? [];
  const racas = dados?.racas ?? [];
  const nomeDoTutorFiltrado = dados?.tutores.find((t) => t.id === tutorFiltrado)?.nome;

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

  /** HU-003, CA-4: pacientes são inativados, nunca excluídos; o prontuário fica preservado. */
  async function alternarStatus(pet: Pet) {
    setErro('');

    if (
      pet.ativo &&
      !(await confirmar({
        titulo: 'Inativar paciente',
        mensagem: (
          <>
            Inativar <strong>{pet.nome}</strong>? O paciente sai das listas ativas, mas o prontuário é preservado e
            ele pode ser reativado depois.
          </>
        ),
        rotuloConfirmar: 'Inativar',
        perigo: true,
      }))
    ) {
      return;
    }

    try {
      await api.patch(`/api/pets/${pet.id}/status`, { ativo: !pet.ativo });
      setAviso(pet.ativo ? `${pet.nome} foi inativado.` : `${pet.nome} foi reativado.`);
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível alterar o status do paciente.'));
    }
  }

  const filtrosAtivos = !!busca || !!tutorFiltrado || !!veterinarioFiltrado || !!racaFiltrada;

  return (
    <>
      <CabecalhoPagina
        titulo="Pacientes"
        descricao={
          ehVeterinario
            ? 'Todos os pacientes da clínica. Use o filtro de veterinário para ver só os seus.'
            : 'Cadastro dos pets em tratamento, com o tutor e o veterinário responsável por cada um.'
        }
        acoes={
          podeCadastrar && (
            <button type="button" className="vc-botao-primario" onClick={() => setCadastroAberto(true)}>
              <Plus size={16} />
              Novo paciente
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
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-64 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
            <input
              className="vc-campo pl-10"
              placeholder="Buscar por nome, raça ou tutor"
              value={busca}
              onChange={(e) => filtrar(() => setBusca(e.target.value))}
              aria-label="Buscar pacientes"
            />
          </div>

          <select
            className="vc-campo w-auto min-w-44"
            value={racaFiltrada}
            onChange={(e) => filtrar(() => setRacaFiltrada(e.target.value))}
            aria-label="Filtrar por raça"
          >
            <option value="">Todas as raças</option>
            {racas.map((raca) => (
              <option key={raca} value={raca}>
                {raca}
              </option>
            ))}
          </select>

          <select
            className="vc-campo w-auto min-w-56"
            value={veterinarioFiltrado}
            onChange={(e) => filtrar(() => setVeterinarioFiltrado(e.target.value))}
            aria-label="Filtrar por veterinário responsável"
          >
            <option value="">Todos os veterinários</option>
            {meuVeterinarioId && <option value={meuVeterinarioId}>Meus pacientes</option>}
            <option value={SEM_RESPONSAVEL}>Sem veterinário responsável</option>
            {veterinarios
              .filter((vet) => vet.id !== meuVeterinarioId)
              .map((vet) => (
                <option key={vet.id} value={vet.id}>
                  {vet.nome}
                </option>
              ))}
          </select>

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
                ? 'Tente outro termo de busca ou limpe os filtros.'
                : 'Cadastre o primeiro paciente para começar a registrar o histórico clínico.'
            }
            acao={
              podeCadastrar && !filtrosAtivos ? (
                <button type="button" className="vc-botao-sutil" onClick={() => setCadastroAberto(true)}>
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
                  <th scope="col">Paciente</th>
                  <th scope="col">Espécie / Raça</th>
                  <th scope="col">Idade</th>
                  <th scope="col">Peso</th>
                  <th scope="col">Tutor</th>
                  <th scope="col">Veterinário</th>
                  <th scope="col">Status</th>
                  <th scope="col" className="text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((pet) => (
                  <tr key={pet.id}>
                    <td>
                      <Link to={`/prontuario/${pet.id}`} className="font-semibold text-slate-900 hover:text-brand">
                        {pet.nome}
                      </Link>
                      {pet.microchip && <span className="block text-xs text-slate-400">Chip {pet.microchip}</span>}
                      {(pet.alertasClinicos.length > 0 || pet.vacinasVencidas > 0) && (
                        <div className="mt-1">
                          <AlertasClinicos alertas={pet.alertasClinicos} vacinasVencidas={pet.vacinasVencidas} compacto />
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
                    <td>
                      {pet.nomeVeterinarioResponsavel ? (
                        pet.nomeVeterinarioResponsavel
                      ) : (
                        <Etiqueta className="bg-alerta-claro text-amber-800">Sem responsável</Etiqueta>
                      )}
                    </td>
                    <td>
                      {pet.dataObito ? (
                        <span title={`Óbito em ${formatarDia(pet.dataObito)}`}>
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
                          title="Visualizar prontuário"
                          aria-label={`Visualizar o prontuário de ${pet.nome}`}
                        >
                          <Eye size={16} />
                        </Link>

                        {podeInativar && !pet.dataObito && (
                          <button
                            type="button"
                            onClick={() => alternarStatus(pet)}
                            className={`rounded-lg p-2 ${
                              pet.ativo ? 'text-slate-500 hover:bg-perigo-claro hover:text-perigo' : 'text-sucesso hover:bg-sucesso-claro'
                            }`}
                            title={pet.ativo ? 'Inativar paciente' : 'Reativar paciente'}
                            aria-label={pet.ativo ? `Inativar ${pet.nome}` : `Reativar ${pet.nome}`}
                          >
                            <Power size={16} />
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

      <ModalCadastroPaciente
        aberto={cadastroAberto}
        tutorInicial={tutorFiltrado}
        veterinarioInicial={veterinarioFiltrado !== SEM_RESPONSAVEL ? veterinarioFiltrado : ''}
        aoFechar={() => setCadastroAberto(false)}
        aoSalvar={(mensagem) => {
          setCadastroAberto(false);
          setAviso(mensagem);
          recarregar();
        }}
      />
    </>
  );
}
