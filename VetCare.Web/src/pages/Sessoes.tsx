import { useCallback, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Activity, ExternalLink, FileText, Power, RotateCcw, X } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import { useCarregamento } from '../hooks/useCarregamento';
import { useConfirmacao } from '../hooks/useConfirmacao';
import { useAtualizacao } from '../contexts/atualizacoes';
import type { Atendimento, GestaoDeSessoes, PetSelecao, Sessao, SituacaoSessao, Tratamento, Veterinario } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { estiloSituacaoSessao, formatarData, formatarIntervalo } from '../utils/formato';
import { paginaVazia } from '../utils/paginacao';
import { ModalRegistrarAtendimento } from './componentes/ModalRegistrarAtendimento';

/** As abas da tela; "Todas" reúne as sessões do plano (sem as inativadas). */
const ABAS: { valor: SituacaoSessao | ''; rotulo: string }[] = [
  { valor: '', rotulo: 'Todas' },
  { valor: 'Agendada', rotulo: 'Agendadas' },
  { valor: 'Aguardando registro', rotulo: 'Aguardando registro' },
  { valor: 'Realizada', rotulo: 'Realizadas' },
  { valor: 'Cancelada', rotulo: 'Canceladas' },
  { valor: 'Inativada', rotulo: 'Inativadas' },
];

const FILTROS_VAZIOS = {
  pacienteId: '',
  tratamentoId: '',
  veterinarioId: '',
  inicio: '',
  fim: '',
};

type Filtros = typeof FILTROS_VAZIOS;

/**
 * Tela "Sessões": a gestão das sessões cadastradas nos tratamentos da clínica, separadas pela
 * situação — as que vão acontecer, as que aguardam o registro do atendimento, as realizadas, as
 * canceladas e as inativadas —, com filtro por animal, tratamento, veterinário e período. Daqui
 * se consulta o registro e se inativa ou reativa uma sessão; as sessões são cadastradas no
 * tratamento, e o atendimento é registrado na agenda ou no prontuário.
 */
export function Sessoes() {
  const { ehVeterinario, usuario } = useAuth();
  const meuVeterinarioId = ehVeterinario ? usuario?.veterinarioId ?? '' : '';
  const [parametros] = useSearchParams();
  const { confirmar, perguntar } = useConfirmacao();

  // Atalhos do prontuário chegam com o animal e o tratamento já escolhidos.
  const [filtros, setFiltros] = useState<Filtros>(() => ({
    ...FILTROS_VAZIOS,
    pacienteId: parametros.get('pacienteId') ?? '',
    tratamentoId: parametros.get('tratamentoId') ?? '',
  }));
  const [situacao, setSituacao] = useState<SituacaoSessao | ''>('');
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [registroAberto, setRegistroAberto] = useState<Atendimento | null>(null);
  const [aviso, setAviso] = useState('');

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
    if (periodoInvalido) return { pagina: paginaVazia<Sessao>(), contagens: {} } as GestaoDeSessoes;

    const { data } = await api.get<GestaoDeSessoes>('/api/sessoes', {
      params: {
        pacienteId: filtros.pacienteId || undefined,
        tratamentoId: filtros.tratamentoId || undefined,
        veterinarioId: filtros.veterinarioId || undefined,
        inicio: filtros.inicio || undefined,
        fim: filtros.fim || undefined,
        situacao: situacao || undefined,
        pagina: numeroPagina,
        tamanho: 20,
      },
    });

    return data;
  }, [filtros, situacao, numeroPagina, periodoInvalido]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(buscar, 'Não foi possível carregar as sessões.', 300);

  useAtualizacao(['sessoes', 'atendimentos', 'tratamentos', 'pets', 'midias'], recarregar);

  const pagina = dados?.pagina ?? paginaVazia<Sessao>();
  const contagens = dados?.contagens ?? {};
  const filtrando = Object.values(filtros).some(Boolean);

  function contagemDa(valor: SituacaoSessao | ''): number | undefined {
    if (!dados) return undefined;
    if (valor) return contagens[valor] ?? 0;

    // "Todas" são as sessões do plano: tudo menos as inativadas.
    return ABAS.filter((a) => a.valor && a.valor !== 'Inativada').reduce((soma, a) => soma + (contagens[a.valor as SituacaoSessao] ?? 0), 0);
  }

  async function abrirRegistro(sessao: Sessao) {
    if (!sessao.atendimentoId) return;

    setErro('');

    try {
      const { data } = await api.get<Atendimento>(`/api/atendimentos/${sessao.atendimentoId}`);
      setRegistroAberto(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível abrir o registro da sessão.'));
    }
  }

  async function inativar(sessao: Sessao) {
    const resposta = await perguntar({
      titulo: 'Inativar sessão',
      mensagem: (
        <>
          Inativar a sessão de <strong>{sessao.nomePaciente}</strong> em {formatarData(sessao.dataHora)}? Ela sai da agenda e
          das contagens do tratamento, mas fica no histórico e pode ser reativada.
        </>
      ),
      rotuloConfirmar: 'Inativar',
      rotuloCancelar: 'Voltar',
      perigo: true,
      campoTexto: { rotulo: 'Motivo', placeholder: 'Ex.: cadastrada em dobro' },
    });

    if (!resposta.confirmado) return;

    try {
      await api.patch(`/api/sessoes/${sessao.id}/inativar`, { motivo: resposta.texto || null });
      setAviso('Sessão inativada. Ela saiu da agenda e pode ser reativada.');
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível inativar a sessão.'));
    }
  }

  async function reativar(sessao: Sessao) {
    if (
      !(await confirmar({
        titulo: 'Reativar sessão',
        mensagem: `Reativar a sessão de ${sessao.nomePaciente} em ${formatarData(sessao.dataHora)}? Ela volta ao plano do tratamento, se o horário ainda estiver livre.`,
        rotuloConfirmar: 'Reativar',
      }))
    ) {
      return;
    }

    try {
      await api.patch(`/api/sessoes/${sessao.id}/reativar`);
      setAviso('Sessão reativada.');
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível reativar a sessão.'));
    }
  }

  /** O veterinário inativa e reativa só as sessões da própria agenda; a API confere de novo. */
  const podeAlterar = (sessao: Sessao) => !ehVeterinario || sessao.veterinarioId === meuVeterinarioId;

  return (
    <>
      <CabecalhoPagina
        titulo="Sessões"
        descricao="As sessões cadastradas nos tratamentos da clínica, separadas pela situação. As sessões são cadastradas no tratamento, no prontuário de cada paciente."
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

      {/* A situação organiza a tela: o que vai acontecer, o que pede registro e o que já foi feito. */}
      <div className="mb-4 flex flex-wrap gap-1 border-b border-slate-200" role="tablist" aria-label="Situação das sessões">
        {ABAS.map((aba) => {
          const contagem = contagemDa(aba.valor);
          const ativa = situacao === aba.valor;

          return (
            <button
              key={aba.rotulo}
              type="button"
              role="tab"
              aria-selected={ativa}
              onClick={() => {
                setSituacao(aba.valor);
                setNumeroPagina(1);
              }}
              className={`-mb-px border-b-2 px-4 py-2.5 text-sm font-medium transition ${
                ativa ? 'border-brand text-brand' : 'border-transparent text-slate-500 hover:text-slate-700'
              }`}
            >
              {aba.rotulo}
              {contagem !== undefined && (
                <span
                  className={`ml-1.5 rounded-full px-1.5 py-0.5 text-xs ${
                    aba.valor === 'Aguardando registro' && contagem > 0
                      ? 'bg-alerta-claro font-semibold text-amber-800'
                      : 'bg-slate-100 text-slate-600'
                  }`}
                >
                  {contagem}
                </span>
              )}
            </button>
          );
        })}
      </div>

      <Card className="mb-6 p-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <label className="text-xs font-semibold text-slate-600">
            Animal
            <select className="vc-campo mt-1" value={filtros.pacienteId} onChange={(e) => filtrar('pacienteId', e.target.value)}>
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
            Veterinário
            <select className="vc-campo mt-1" value={filtros.veterinarioId} onChange={(e) => filtrar('veterinarioId', e.target.value)}>
              <option value="">Todos</option>
              {meuVeterinarioId && <option value={meuVeterinarioId}>Minhas sessões</option>}
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
            De
            <input type="date" className="vc-campo mt-1" value={filtros.inicio} onChange={(e) => filtrar('inicio', e.target.value)} />
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
        <Carregando texto="Carregando sessões..." />
      ) : pagina.itens.length === 0 ? (
        <Card>
          <SemDados
            icone={<Activity size={40} />}
            titulo={filtrando || situacao ? 'Nenhuma sessão encontrada' : 'Nenhuma sessão cadastrada'}
            descricao={
              filtrando || situacao
                ? 'Ajuste os filtros ou escolha outra situação para ver mais sessões.'
                : 'As sessões são cadastradas no tratamento, no prontuário de cada paciente, uma ou várias de uma vez.'
            }
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          {/* "relative" prende o rótulo sr-only do cabeçalho de ações dentro da rolagem da tabela;
              sem isso ele escapa da caixa e cria rolagem horizontal na página do celular. */}
          <div className="relative overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th scope="col">Data</th>
                  <th scope="col">Animal</th>
                  <th scope="col">Tratamento</th>
                  <th scope="col">Veterinário</th>
                  <th scope="col">Situação</th>
                  <th scope="col">
                    <span className="sr-only">Ações</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((sessao) => (
                  <tr key={sessao.id} className={sessao.inativa ? 'text-slate-400' : undefined}>
                    <td className="whitespace-nowrap">
                      <span className="block font-medium text-slate-900">{formatarData(sessao.dataHora)}</span>
                      <span className="block text-xs text-slate-500">{formatarIntervalo(sessao.dataHora, sessao.dataHoraFim)}</span>
                    </td>
                    <td>
                      <Link to={`/prontuario/${sessao.pacienteId}`} className="font-semibold text-slate-900 hover:text-brand">
                        {sessao.nomePaciente}
                      </Link>
                      <span className="block text-xs text-slate-500">{sessao.nomeTutor}</span>
                    </td>
                    <td className="max-w-xs">
                      <span className="line-clamp-1">{sessao.nomeTratamento}</span>
                      {sessao.numeroSessao && (
                        <span className="line-clamp-1 text-xs text-slate-500">
                          Sessão nº {sessao.numeroSessao}
                          {sessao.escalaDor != null && ` · dor ${sessao.escalaDor}/10`}
                          {sessao.tecnicasAplicadas && ` · ${sessao.tecnicasAplicadas}`}
                        </span>
                      )}
                    </td>
                    <td>{sessao.nomeVeterinario}</td>
                    <td>
                      <Etiqueta className={estiloSituacaoSessao[sessao.situacao]}>{sessao.situacao}</Etiqueta>
                    </td>
                    <td className="whitespace-nowrap text-right">
                      <span className="inline-flex items-center gap-1">
                        {sessao.atendimentoId && (
                          <button
                            type="button"
                            onClick={() => abrirRegistro(sessao)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 hover:text-brand"
                            title="Ver o registro do atendimento"
                            aria-label={`Ver o registro da sessão de ${sessao.nomePaciente}`}
                          >
                            <FileText size={16} />
                          </button>
                        )}

                        {!sessao.inativa && !sessao.possuiAtendimento && sessao.status !== 'Concluída' && podeAlterar(sessao) && (
                          <button
                            type="button"
                            onClick={() => inativar(sessao)}
                            className="rounded-lg p-2 text-slate-400 hover:bg-perigo-claro hover:text-perigo"
                            title="Inativar sessão"
                            aria-label={`Inativar a sessão de ${sessao.nomePaciente}`}
                          >
                            <Power size={16} />
                          </button>
                        )}

                        {sessao.inativa && podeAlterar(sessao) && (
                          <button
                            type="button"
                            onClick={() => reativar(sessao)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
                            title="Reativar sessão"
                            aria-label={`Reativar a sessão de ${sessao.nomePaciente}`}
                          >
                            <RotateCcw size={16} />
                          </button>
                        )}

                        <Link
                          to={`/prontuario/${sessao.pacienteId}?aba=tratamentos&tratamento=${sessao.tratamentoId}`}
                          className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 hover:text-brand"
                          title="Abrir o tratamento no prontuário"
                          aria-label={`Abrir o tratamento de ${sessao.nomePaciente} no prontuário`}
                        >
                          <ExternalLink size={16} />
                        </Link>
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="sessões" />
        </Card>
      )}

      <ModalRegistrarAtendimento
        key={registroAberto?.id ?? 'nenhum'}
        atendimento={registroAberto}
        exibirLinkDoProntuario
        aoFechar={() => setRegistroAberto(null)}
        aoSalvar={() => {
          setRegistroAberto(null);
          recarregar();
        }}
      />
    </>
  );
}
