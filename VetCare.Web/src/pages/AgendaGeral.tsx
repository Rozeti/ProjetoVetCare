import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { CalendarRange, ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { api } from '../services/api';
import type { AgendaGeral as AgendaGeralDTO, VisaoAgenda } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { estiloStatusSessao, formatarData, formatarHora, paraValorInputData } from '../utils/formato';
import { ModalAgendarSessao } from './componentes/ModalAgendarSessao';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';

const VISOES: { valor: VisaoAgenda; rotulo: string }[] = [
  { valor: 'dia', rotulo: 'Dia' },
  { valor: 'semana', rotulo: 'Semana' },
  { valor: 'mes', rotulo: 'Mês' },
];

/** HU-005: agenda geral da clínica, com todos os veterinários no mesmo período. */
export function AgendaGeral() {
  const [visao, setVisao] = useState<VisaoAgenda>('dia');
  const [data, setData] = useState(new Date());
  const [modalAgendar, setModalAgendar] = useState(false);

  /** HU-005, CA-2: toggle que exibe ou oculta a agenda de veterinários específicos. */
  const [ocultos, setOcultos] = useState<Set<string>>(new Set());

  const buscar = useCallback(async () => {
    const { data: resposta } = await api.get<AgendaGeralDTO>('/api/sessoes/agenda-geral', {
      params: { data: paraValorInputData(data), visao },
    });

    return resposta;
  }, [data, visao]);

  const {
    dados: agenda,
    carregando,
    erro,
    setErro,
    recarregar,
  } = useCarregamento(buscar, 'Não foi possível carregar a agenda geral.');

  // HU-006: a confirmação e o cancelamento feitos pelo tutor mudam esta tela sozinhos.
  useAtualizacao(['sessoes', 'tratamentos', 'bloqueiosagenda', 'atendimentos'], recarregar);

  function navegar(passo: number) {
    const nova = new Date(data);

    if (visao === 'dia') nova.setDate(nova.getDate() + passo);
    else if (visao === 'semana') nova.setDate(nova.getDate() + passo * 7);
    else nova.setMonth(nova.getMonth() + passo);

    setData(nova);
  }

  function alternarVeterinario(id: string) {
    setOcultos((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  // A filtragem acontece no cliente para que o toggle responda instantaneamente.
  const sessoesVisiveis = useMemo(
    () => (agenda?.sessoes ?? []).filter((s) => !ocultos.has(s.veterinarioId)),
    [agenda, ocultos],
  );

  const porDia = useMemo(() => {
    const grupos = new Map<string, typeof sessoesVisiveis>();

    for (const sessao of sessoesVisiveis) {
      const chave = paraValorInputData(sessao.dataHora);
      grupos.set(chave, [...(grupos.get(chave) ?? []), sessao]);
    }

    return [...grupos.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [sessoesVisiveis]);

  const titulo = useMemo(() => {
    if (visao === 'dia') {
      return data.toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' });
    }

    if (visao === 'mes') {
      return data.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
    }

    const inicio = new Date(data);
    inicio.setDate(inicio.getDate() - inicio.getDay());
    const fim = new Date(inicio);
    fim.setDate(fim.getDate() + 6);

    return `${formatarData(inicio)} — ${formatarData(fim)}`;
  }, [data, visao]);

  return (
    <>
      <CabecalhoPagina
        titulo="Agenda geral da clínica"
        descricao="Visão única de todos os profissionais no mesmo período."
        acoes={
          <button type="button" className="vc-botao-primario" onClick={() => setModalAgendar(true)}>
            <Plus size={16} />
            Agendar sessão
          </button>
        }
      />

      {erro && (
        <div className="mb-4">
          <Alerta tipo="erro" aoFechar={() => setErro('')}>
            {erro}
          </Alerta>
        </div>
      )}

      <Card className="mb-6 p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-1">
            <button
              type="button"
              onClick={() => navegar(-1)}
              className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
              aria-label="Período anterior"
            >
              <ChevronLeft size={18} />
            </button>

            <span className="min-w-56 text-center text-sm font-semibold capitalize text-slate-900">{titulo}</span>

            <button
              type="button"
              onClick={() => navegar(1)}
              className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
              aria-label="Próximo período"
            >
              <ChevronRight size={18} />
            </button>

            <button type="button" onClick={() => setData(new Date())} className="ml-2 text-sm font-medium text-brand hover:underline">
              Hoje
            </button>
          </div>

          <div className="flex items-center gap-3">
            <input
              type="date"
              className="vc-campo w-auto py-2"
              value={paraValorInputData(data)}
              onChange={(e) => e.target.value && setData(new Date(`${e.target.value}T12:00:00`))}
              aria-label="Escolher data"
            />

            <div className="flex rounded-xl border border-slate-300 p-0.5" role="tablist" aria-label="Visão da agenda">
              {VISOES.map((item) => (
                <button
                  key={item.valor}
                  type="button"
                  role="tab"
                  aria-selected={visao === item.valor}
                  onClick={() => setVisao(item.valor)}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium transition ${
                    visao === item.valor ? 'bg-brand text-white' : 'text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {item.rotulo}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* HU-005, CA-1 e CA-2: legenda por cor com toggle de profissionais. */}
        {agenda && agenda.veterinarios.length > 0 && (
          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-4">
            <span className="mr-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Profissionais</span>

            {agenda.veterinarios.map((vet) => {
              const oculto = ocultos.has(vet.id);

              return (
                <button
                  key={vet.id}
                  type="button"
                  onClick={() => alternarVeterinario(vet.id)}
                  aria-pressed={!oculto}
                  className={`flex items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-medium transition ${
                    oculto
                      ? 'border-slate-200 bg-slate-50 text-slate-400 line-through'
                      : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
                  }`}
                >
                  <span
                    className="h-2.5 w-2.5 rounded-full"
                    style={{ backgroundColor: oculto ? '#cbd5e1' : vet.cor }}
                    aria-hidden="true"
                  />
                  {vet.nome}
                </button>
              );
            })}
          </div>
        )}
      </Card>

      {carregando ? (
        <Carregando texto="Carregando agenda geral..." />
      ) : sessoesVisiveis.length === 0 ? (
        /* HU-005, CA-3: dia sem sessões é informado, sem tela de erro. */
        <Card>
          <SemDados
            icone={<CalendarRange size={40} />}
            titulo="Não há atendimentos neste período"
            descricao={
              ocultos.size > 0
                ? 'Nenhuma sessão para os profissionais atualmente exibidos. Reative algum profissional na legenda acima.'
                : 'Nenhuma sessão foi agendada para a data selecionada.'
            }
          />
        </Card>
      ) : (
        <div className="space-y-6">
          {porDia.map(([dia, itens]) => (
            <Card key={dia}>
              <div className="border-b border-slate-200 px-5 py-3">
                <h2 className="text-sm font-semibold capitalize text-slate-700">
                  {new Date(`${dia}T12:00:00`).toLocaleDateString('pt-BR', {
                    weekday: 'long',
                    day: '2-digit',
                    month: 'long',
                  })}
                  <span className="ml-2 font-normal text-slate-400">
                    ({itens.length} {itens.length === 1 ? 'sessão' : 'sessões'})
                  </span>
                </h2>
              </div>

              <ul className="divide-y divide-slate-100">
                {itens.map((sessao) => (
                  <li key={sessao.sessaoId} className="flex flex-wrap items-center gap-4 px-5 py-4">
                    <span
                      className="h-10 w-1.5 shrink-0 rounded-full"
                      style={{ backgroundColor: sessao.corVeterinario }}
                      aria-hidden="true"
                    />

                    <div className="w-16 shrink-0 text-center">
                      <p className="text-sm font-bold text-slate-900">{formatarHora(sessao.dataHora)}</p>
                    </div>

                    <div className="min-w-0 flex-1">
                      <Link
                        to={`/prontuario/${sessao.pacienteId}`}
                        className="font-semibold text-slate-900 hover:text-brand"
                      >
                        {sessao.nomePaciente}
                      </Link>
                      <p className="truncate text-xs text-slate-500">
                        {sessao.nomeVeterinario}
                        {sessao.nomeTutor && ` · Tutor: ${sessao.nomeTutor}`}
                      </p>
                    </div>

                    <Etiqueta className={estiloStatusSessao[sessao.status]}>{sessao.status}</Etiqueta>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}

      <ModalAgendarSessao
        aberto={modalAgendar}
        aoFechar={() => setModalAgendar(false)}
        aoSalvar={() => {
          setModalAgendar(false);
          recarregar();
        }}
      />
    </>
  );
}
