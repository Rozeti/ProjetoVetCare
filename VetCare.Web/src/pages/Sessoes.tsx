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
import { estiloSituacaoSessao, formatarData, formatarDataExtensa, formatarHora, paraValorInputData } from '../utils/formato';
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

/** Sessões da página agrupadas pelo dia, na ordem em que a API as entrega. */
function agruparPorDia(sessoes: Sessao[]): { dia: string; sessoes: Sessao[] }[] {
  const grupos: { dia: string; sessoes: Sessao[] }[] = [];

  for (const sessao of sessoes) {
    const dia = paraValorInputData(sessao.dataHora);
    const ultimo = grupos[grupos.length - 1];

    if (ultimo?.dia === dia) ultimo.sessoes.push(sessao);
    else grupos.push({ dia, sessoes: [sessao] });
  }

  return grupos;
}

/** "Hoje", "Amanhã" ou "Ontem", quando o dia é um desses. */
function diaRelativo(dia: string): string {
  const hoje = new Date();
  const deslocado = (dias: number) => paraValorInputData(new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate() + dias));

  if (dia === deslocado(0)) return 'Hoje';
  if (dia === deslocado(1)) return 'Amanhã';
  if (dia === deslocado(-1)) return 'Ontem';
  return '';
}

/**
 * Tela "Sessões": a gestão das sessões cadastradas nos tratamentos da clínica, separadas pela
 * situação — as que vão acontecer, as que aguardam o registro do atendimento, as realizadas, as
 * canceladas e as inativadas —, com filtro por animal, tratamento, veterinário e período. As
 * sessões aparecem agrupadas por dia, uma linha cada, com o horário, o animal, o tratamento, o
 * veterinário e a situação. Daqui se consulta o registro e se inativa ou reativa uma sessão; as
 * sessões são cadastradas no tratamento, e o atendimento é registrado na agenda ou no prontuário.
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

  const icone = 'rounded-lg p-2 text-slate-400 transition hover:bg-slate-100 hover:text-brand';

  return (
    <>
      <CabecalhoPagina
        titulo="Sessões"
        descricao="As sessões dos tratamentos da clínica, por situação. Novas sessões são cadastradas no tratamento, no prontuário do paciente."
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

      <Card className="overflow-hidden">
        {/* A situação organiza a tela: o que vai acontecer, o que pede registro e o que já foi feito. */}
        <div className="flex gap-1 overflow-x-auto border-b border-slate-200 px-3" role="tablist" aria-label="Situação das sessões">
          {ABAS.map((aba) => {
            const contagem = contagemDa(aba.valor);
            const ativa = situacao === aba.valor;
            const pendente = aba.valor === 'Aguardando registro' && !!contagem;

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
                className={`-mb-px inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap border-b-2 px-3 py-3 text-sm font-medium transition ${
                  ativa ? 'border-brand text-brand' : 'border-transparent text-slate-500 hover:text-slate-800'
                }`}
              >
                {aba.rotulo}
                {contagem !== undefined && (
                  <span
                    className={`rounded-full px-1.5 py-0.5 text-xs tabular-nums ${
                      pendente
                        ? 'bg-alerta-claro font-semibold text-amber-800'
                        : ativa
                          ? 'bg-brand-100 text-brand-dark'
                          : 'bg-slate-100 text-slate-500'
                    }`}
                  >
                    {contagem}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {/* Filtros numa barra só; o tratamento aparece depois de escolhido o animal. */}
        <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 bg-slate-50/70 px-4 py-3">
          <select
            className="vc-campo w-full py-2 sm:w-auto sm:max-w-56"
            value={filtros.pacienteId}
            onChange={(e) => filtrar('pacienteId', e.target.value)}
            aria-label="Animal"
          >
            <option value="">Todos os animais</option>
            {opcoes?.pacientes.map((paciente) => (
              <option key={paciente.id} value={paciente.id}>
                {paciente.nome} — {paciente.nomeTutor}
              </option>
            ))}
          </select>

          {filtros.pacienteId && (
            <select
              className="vc-campo w-full py-2 sm:w-auto sm:max-w-64"
              value={filtros.tratamentoId}
              onChange={(e) => filtrar('tratamentoId', e.target.value)}
              aria-label="Tratamento"
            >
              <option value="">Todos os tratamentos</option>
              {tratamentos?.map((tratamento) => (
                <option key={tratamento.id} value={tratamento.id}>
                  {tratamento.nome} ({tratamento.status.toLowerCase()})
                </option>
              ))}
            </select>
          )}

          <select
            className="vc-campo w-full py-2 sm:w-auto sm:max-w-56"
            value={filtros.veterinarioId}
            onChange={(e) => filtrar('veterinarioId', e.target.value)}
            aria-label="Veterinário"
          >
            <option value="">Todos os veterinários</option>
            {meuVeterinarioId && <option value={meuVeterinarioId}>Minhas sessões</option>}
            {opcoes?.veterinarios
              .filter((vet) => vet.id !== meuVeterinarioId)
              .map((vet) => (
                <option key={vet.id} value={vet.id}>
                  {vet.nome}
                </option>
              ))}
          </select>

          <span className="flex w-full items-center gap-2 text-sm text-slate-500 sm:w-auto">
            <input
              type="date"
              className="vc-campo min-w-0 flex-1 py-2 sm:w-auto sm:flex-none"
              value={filtros.inicio}
              onChange={(e) => filtrar('inicio', e.target.value)}
              aria-label="De"
              title="De"
            />
            até
            <input
              type="date"
              className="vc-campo min-w-0 flex-1 py-2 sm:w-auto sm:flex-none"
              min={filtros.inicio || undefined}
              value={filtros.fim}
              onChange={(e) => filtrar('fim', e.target.value)}
              aria-label="Até"
              title="Até"
            />
          </span>

          {filtrando && (
            <button
              type="button"
              onClick={() => {
                setFiltros(FILTROS_VAZIOS);
                setNumeroPagina(1);
              }}
              className="inline-flex items-center gap-1 rounded-lg px-2 py-2 text-sm font-medium text-brand hover:bg-brand-50"
            >
              <X size={14} />
              Limpar filtros
            </button>
          )}

          {periodoInvalido && <p className="w-full text-sm text-perigo">A data final não pode ser anterior à inicial.</p>}
        </div>

        {carregando ? (
          <Carregando texto="Carregando sessões..." />
        ) : pagina.itens.length === 0 ? (
          <SemDados
            icone={<Activity size={40} />}
            titulo={filtrando || situacao ? 'Nenhuma sessão encontrada' : 'Nenhuma sessão cadastrada'}
            descricao={
              filtrando || situacao
                ? 'Ajuste os filtros ou escolha outra situação para ver mais sessões.'
                : 'As sessões são cadastradas no tratamento, no prontuário de cada paciente, uma ou várias de uma vez.'
            }
          />
        ) : (
          <>
            {agruparPorDia(pagina.itens).map(({ dia, sessoes }) => {
              const relativo = diaRelativo(dia);
              const extenso = formatarDataExtensa(`${dia}T12:00:00`);

              return (
                <section key={dia} aria-label={extenso}>
                  <h3 className="flex items-center gap-2 border-b border-slate-100 bg-slate-50 px-4 py-2 sm:px-5 text-xs font-semibold text-slate-600">
                    <span className="inline-block first-letter:uppercase">{extenso}</span>
                    {relativo && <Etiqueta className="bg-brand-100 py-0.5 text-brand-dark">{relativo}</Etiqueta>}
                    <span className="ml-auto whitespace-nowrap font-normal text-slate-400">
                      {sessoes.length} {sessoes.length === 1 ? 'sessão' : 'sessões'}
                    </span>
                  </h3>

                  <ul className="divide-y divide-slate-100">
                    {sessoes.map((sessao) => (
                      <li
                        key={sessao.id}
                        className={`flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 sm:px-5 transition hover:bg-slate-50/80 ${
                          sessao.inativa ? 'text-slate-400' : ''
                        }`}
                      >
                        <span className="w-14 shrink-0 tabular-nums">
                          <span className={`block text-sm font-semibold ${sessao.inativa ? 'line-through' : 'text-slate-900'}`}>
                            {formatarHora(sessao.dataHora)}
                          </span>
                          <span className="block text-xs text-slate-500">até {formatarHora(sessao.dataHoraFim)}</span>
                        </span>

                        <span className="min-w-0 flex-1 basis-52">
                          <span className="block truncate text-sm">
                            <Link to={`/prontuario/${sessao.pacienteId}`} className="font-semibold text-slate-900 hover:text-brand">
                              {sessao.nomePaciente}
                            </Link>
                            <span className="text-slate-500"> · tutor {sessao.nomeTutor}</span>
                          </span>
                          <span className="block truncate text-xs text-slate-500" title={sessao.nomeTratamento}>
                            {[
                              sessao.nomeTratamento,
                              sessao.numeroSessao && `sessão nº ${sessao.numeroSessao}`,
                              sessao.escalaDor != null && `dor ${sessao.escalaDor}/10`,
                              sessao.tecnicasAplicadas,
                            ]
                              .filter(Boolean)
                              .join(' · ')}
                            <span className="xl:hidden"> · {sessao.nomeVeterinario}</span>
                          </span>
                        </span>

                        <span className="hidden w-44 shrink-0 truncate text-sm text-slate-600 xl:block" title={sessao.nomeVeterinario}>
                          {sessao.nomeVeterinario}
                        </span>

                        <span className="shrink-0 sm:w-40">
                          <Etiqueta className={estiloSituacaoSessao[sessao.situacao]}>{sessao.situacao}</Etiqueta>
                        </span>

                        <span className="ml-auto flex shrink-0 items-center gap-0.5">
                          {sessao.atendimentoId && (
                            <button
                              type="button"
                              onClick={() => abrirRegistro(sessao)}
                              className={icone}
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
                              className="rounded-lg p-2 text-slate-400 transition hover:bg-perigo-claro hover:text-perigo"
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
                              className={icone}
                              title="Reativar sessão"
                              aria-label={`Reativar a sessão de ${sessao.nomePaciente}`}
                            >
                              <RotateCcw size={16} />
                            </button>
                          )}

                          <Link
                            to={`/prontuario/${sessao.pacienteId}?aba=tratamentos&tratamento=${sessao.tratamentoId}`}
                            className={icone}
                            title="Abrir o tratamento no prontuário"
                            aria-label={`Abrir o tratamento de ${sessao.nomePaciente} no prontuário`}
                          >
                            <ExternalLink size={16} />
                          </Link>
                        </span>
                      </li>
                    ))}
                  </ul>
                </section>
              );
            })}
          </>
        )}

        <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="sessões" />
      </Card>

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
