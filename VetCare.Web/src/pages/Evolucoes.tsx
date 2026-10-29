import { useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Activity, ClipboardPlus, Pencil, X } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import type { Atendimento, EvolucaoResumo, PaginaDe, PetSelecao, Tratamento, Veterinario } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { estiloEscalaDor, formatarDataHora } from '../utils/formato';
import { paginaVazia } from '../utils/paginacao';
import { ModalRegistrarAtendimento } from './componentes/ModalRegistrarAtendimento';
import { ModalEscolherTratamento } from './componentes/ModalEscolherTratamento';

const FILTROS_VAZIOS = {
  pacienteId: '',
  tratamentoId: '',
  inicio: '',
  fim: '',
  veterinarioId: '',
  numeroSessao: '',
};

type Filtros = typeof FILTROS_VAZIOS;

/**
 * Evoluções registradas nas sessões dos tratamentos, com filtro por animal, tratamento,
 * período, veterinário e número da sessão. Administração e qualquer veterinário veem as
 * evoluções de todos os pacientes da clínica. Filtradas por tratamento, saem em ordem cronológica.
 */
export function Evolucoes() {
  const { ehVeterinario, usuario, temPerfil } = useAuth();
  const meuVeterinarioId = ehVeterinario ? usuario?.veterinarioId ?? '' : '';
  const podeRegistrar = temPerfil('Administrador', 'Veterinario');
  const [parametros] = useSearchParams();

  // Atalhos do prontuário chegam com o animal e o tratamento já escolhidos.
  const [filtros, setFiltros] = useState<Filtros>(() => ({
    ...FILTROS_VAZIOS,
    pacienteId: parametros.get('pacienteId') ?? '',
    tratamentoId: parametros.get('tratamentoId') ?? '',
  }));
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [evolucaoAberta, setEvolucaoAberta] = useState<Atendimento | null>(null);
  // Registro de uma sessão realizada: primeiro o animal e o tratamento, depois a evolução.
  const [escolhendoTratamento, setEscolhendoTratamento] = useState(false);
  const [sessaoDoTratamento, setSessaoDoTratamento] = useState<{ tratamento: Tratamento; descricao: string } | null>(null);

  const periodoInvalido = !!filtros.inicio && !!filtros.fim && filtros.fim < filtros.inicio;

  function filtrar(campo: keyof Filtros, valor: string) {
    setFiltros((atual) => ({
      ...atual,
      [campo]: valor,
      // Trocar de animal invalida o tratamento escolhido: ele é de outro paciente.
      ...(campo === 'pacienteId' ? { tratamentoId: '' } : {}),
    }));
    setNumeroPagina(1);
  }

  const buscarOpcoes = useCallback(async () => {
    const [pacientes, veterinarios] = await Promise.all([
      api.get<PetSelecao[]>('/api/pets/selecao'),
      api.get<Veterinario[]>('/api/veterinarios'),
    ]);

    return { pacientes: pacientes.data, veterinarios: veterinarios.data };
  }, []);

  const { dados: opcoes } = useCarregamento(buscarOpcoes, 'Não foi possível carregar os filtros.');

  const buscarTratamentos = useCallback(async () => {
    if (!filtros.pacienteId) return [] as Tratamento[];

    const { data } = await api.get<Tratamento[]>(`/api/tratamentos/paciente/${filtros.pacienteId}`);
    return data;
  }, [filtros.pacienteId]);

  const { dados: tratamentos } = useCarregamento(buscarTratamentos, 'Não foi possível carregar os tratamentos.');

  const buscar = useCallback(async () => {
    if (periodoInvalido) return paginaVazia<EvolucaoResumo>();

    const { data } = await api.get<PaginaDe<EvolucaoResumo>>('/api/atendimentos', {
      params: {
        pacienteId: filtros.pacienteId || undefined,
        tratamentoId: filtros.tratamentoId || undefined,
        inicio: filtros.inicio || undefined,
        fim: filtros.fim || undefined,
        veterinarioId: filtros.veterinarioId || undefined,
        numeroSessao: filtros.numeroSessao || undefined,
        pagina: numeroPagina,
        tamanho: 20,
      },
    });

    return data;
  }, [filtros, numeroPagina, periodoInvalido]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar as evoluções.',
    300,
  );

  useAtualizacao(['atendimentos', 'tratamentos', 'pets', 'midias'], recarregar);

  const pagina = dados ?? paginaVazia<EvolucaoResumo>();
  const filtrando = Object.values(filtros).some(Boolean);

  async function abrir(evolucao: EvolucaoResumo) {
    setErro('');

    try {
      const { data } = await api.get<Atendimento>(`/api/atendimentos/${evolucao.id}`);
      setEvolucaoAberta(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível abrir a evolução.'));
    }
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Evoluções"
        descricao="Evoluções registradas nas sessões dos tratamentos da clínica, uma por sessão realizada."
        acoes={
          podeRegistrar && (
            <button type="button" className="vc-botao-primario" onClick={() => setEscolhendoTratamento(true)}>
              <ClipboardPlus size={16} />
              Registrar sessão
            </button>
          )
        }
      />

      {erro && (
        <Alerta flutuante tipo="erro" aoFechar={() => setErro('')}>
          {erro}
        </Alerta>
      )}

      <Card className="mb-6 p-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          <label className="text-xs font-semibold text-slate-600">
            Animal
            <select
              className="vc-campo mt-1"
              value={filtros.pacienteId}
              onChange={(e) => filtrar('pacienteId', e.target.value)}
            >
              <option value="">Todos os animais</option>
              {opcoes?.pacientes.map((paciente) => (
                <option key={paciente.id} value={paciente.id}>
                  {paciente.nome} — {paciente.nomeTutor}
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Tratamento
            <select
              className="vc-campo mt-1"
              value={filtros.tratamentoId}
              onChange={(e) => filtrar('tratamentoId', e.target.value)}
              disabled={!filtros.pacienteId}
              title={filtros.pacienteId ? undefined : 'Escolha o animal para filtrar por tratamento'}
            >
              <option value="">{filtros.pacienteId ? 'Todos os tratamentos' : 'Escolha o animal'}</option>
              {tratamentos?.map((tratamento) => (
                <option key={tratamento.id} value={tratamento.id}>
                  {tratamento.nome} ({tratamento.status.toLowerCase()})
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs font-semibold text-slate-600">
            De
            <input
              type="date"
              className="vc-campo mt-1"
              value={filtros.inicio}
              onChange={(e) => filtrar('inicio', e.target.value)}
            />
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Até
            <input
              type="date"
              className="vc-campo mt-1"
              min={filtros.inicio || undefined}
              value={filtros.fim}
              onChange={(e) => filtrar('fim', e.target.value)}
            />
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Veterinário
            <select
              className="vc-campo mt-1"
              value={filtros.veterinarioId}
              onChange={(e) => filtrar('veterinarioId', e.target.value)}
            >
              <option value="">Todos</option>
              {meuVeterinarioId && <option value={meuVeterinarioId}>Registradas por mim</option>}
              {opcoes?.veterinarios
                .filter((vet) => vet.id !== meuVeterinarioId)
                .map((vet) => (
                  <option key={vet.id} value={vet.id}>
                    {vet.nome}
                  </option>
                ))}
            </select>
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Nº da sessão
            <input
              type="number"
              min={1}
              className="vc-campo mt-1"
              value={filtros.numeroSessao}
              onChange={(e) => filtrar('numeroSessao', e.target.value)}
              placeholder="Todas"
            />
          </label>
        </div>

        {(filtrando || periodoInvalido) && (
          <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
            {periodoInvalido && <span className="text-perigo">A data final não pode ser anterior à inicial.</span>}
            {filtrando && (
              <button
                type="button"
                onClick={() => {
                  setFiltros(FILTROS_VAZIOS);
                  setNumeroPagina(1);
                }}
                className="inline-flex items-center gap-1 font-medium text-brand hover:underline"
              >
                <X size={14} />
                Limpar filtros
              </button>
            )}
          </div>
        )}
      </Card>

      {carregando ? (
        <Carregando texto="Carregando evoluções..." />
      ) : pagina.itens.length === 0 ? (
        <Card>
          <SemDados
            icone={<Activity size={40} />}
            titulo={filtrando ? 'Nenhuma evolução encontrada' : 'Nenhuma evolução registrada'}
            descricao={
              filtrando
                ? 'Ajuste ou limpe os filtros para ver outras evoluções.'
                : 'Cada sessão realizada num tratamento gera uma evolução. Registre-a em “Registrar sessão”, no tratamento do prontuário ou pela sessão na Agenda.'
            }
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th scope="col">Data</th>
                  <th scope="col">Animal</th>
                  <th scope="col">Tratamento</th>
                  <th scope="col">Sessão</th>
                  <th scope="col">Procedimentos e reação</th>
                  <th scope="col">Dor</th>
                  <th scope="col">Responsável</th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((evolucao) => (
                  <tr
                    key={evolucao.id}
                    onClick={() => abrir(evolucao)}
                    className="cursor-pointer hover:bg-slate-50"
                    title="Abrir a evolução"
                  >
                    <td className="whitespace-nowrap">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          abrir(evolucao);
                        }}
                        className="text-left font-medium text-slate-900 hover:text-brand"
                      >
                        {formatarDataHora(evolucao.dataRegistro)}
                      </button>
                      {evolucao.editado && (
                        <Etiqueta className="mt-1 bg-alerta-claro text-amber-800">
                          <Pencil size={11} />
                          Editado
                        </Etiqueta>
                      )}
                    </td>
                    <td>
                      <Link
                        to={`/prontuario/${evolucao.pacienteId}`}
                        onClick={(e) => e.stopPropagation()}
                        className="font-semibold text-slate-900 hover:text-brand"
                      >
                        {evolucao.nomePaciente}
                      </Link>
                    </td>
                    <td>{evolucao.nomeTratamento}</td>
                    <td className="whitespace-nowrap">nº {evolucao.numeroSessao}</td>
                    <td className="max-w-sm">
                      <span className="line-clamp-1 text-slate-800">{evolucao.tecnicasAplicadas}</span>
                      <span className="line-clamp-1 text-xs text-slate-500">{evolucao.reacaoAnimal}</span>
                    </td>
                    <td>
                      <Etiqueta className={estiloEscalaDor(evolucao.escalaDor)}>{evolucao.escalaDor}/10</Etiqueta>
                    </td>
                    <td>{evolucao.nomeVeterinario}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="evoluções" />
        </Card>
      )}

      <ModalEscolherTratamento
        aberto={escolhendoTratamento}
        pacientes={opcoes?.pacientes ?? []}
        pacienteInicial={filtros.pacienteId}
        tratamentoInicial={filtros.tratamentoId}
        aoFechar={() => setEscolhendoTratamento(false)}
        aoEscolher={(tratamento, paciente) => {
          setEscolhendoTratamento(false);
          setSessaoDoTratamento({ tratamento, descricao: `${paciente.nome} · ${paciente.nomeTutor}` });
        }}
      />

      <ModalRegistrarAtendimento
        key={sessaoDoTratamento?.tratamento.id ?? evolucaoAberta?.id ?? 'nenhuma'}
        atendimento={evolucaoAberta}
        tratamento={sessaoDoTratamento?.tratamento}
        descricao={sessaoDoTratamento?.descricao}
        exibirLinkDoProntuario
        aoFechar={() => {
          setEvolucaoAberta(null);
          setSessaoDoTratamento(null);
        }}
        aoSalvar={() => {
          setEvolucaoAberta(null);
          setSessaoDoTratamento(null);
          recarregar();
        }}
      />
    </>
  );
}
