import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarDays,
  CalendarOff,
  Check,
  ChevronLeft,
  ChevronRight,
  ClipboardPlus,
  Plus,
  Stethoscope,
  X,
} from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import type { ItemAgenda, VisaoAgenda } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { estiloStatusSessao, formatarData, formatarHora, paraValorInputData } from '../utils/formato';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { ModalAgendarSessao } from './componentes/ModalAgendarSessao';
import { ModalRegistrarAtendimento } from './componentes/ModalRegistrarAtendimento';
import { ModalBloqueiosAgenda } from './componentes/ModalBloqueiosAgenda';

const VISOES: { valor: VisaoAgenda; rotulo: string }[] = [
  { valor: 'dia', rotulo: 'Dia' },
  { valor: 'semana', rotulo: 'Semana' },
  { valor: 'mes', rotulo: 'Mês' },
];

/** HU-004: agenda individual do veterinário com visualização por Dia, Semana e Mês. */
export function Agenda() {
  const { usuario } = useAuth();

  const [visao, setVisao] = useState<VisaoAgenda>('dia');
  const [data, setData] = useState(new Date());
  const [aviso, setAviso] = useState('');
  const [modalAgendar, setModalAgendar] = useState(false);
  const [modalBloqueios, setModalBloqueios] = useState(false);
  const [sessaoParaAtender, setSessaoParaAtender] = useState<ItemAgenda | null>(null);

  const veterinarioId = usuario?.veterinarioId ?? '';

  const buscar = useCallback(async () => {
    if (!veterinarioId) {
      return [];
    }

    const { data: itens } = await api.get<ItemAgenda[]>(
      `/api/sessoes/agenda/${veterinarioId}`,
      { params: { data: paraValorInputData(data), visao } },
    );

    return itens;
  }, [veterinarioId, data, visao]);

  const {
    dados,
    carregando,
    erro: erroDaBusca,
    setErro,
    recarregar,
  } = useCarregamento<ItemAgenda[]>(buscar, 'Não foi possível carregar a agenda.');

  // HU-006: a confirmação e o cancelamento feitos pelo tutor mudam esta tela sozinhos.
  useAtualizacao(['sessoes', 'tratamentos', 'bloqueiosagenda', 'atendimentos'], recarregar);

  // Estabiliza a referência para os useMemo que agrupam a agenda por dia.
  const sessoes = useMemo(() => dados ?? [], [dados]);

  // Um usuário sem cadastro de veterinário não tem agenda própria para exibir.
  const erro = veterinarioId ? erroDaBusca : 'Seu usuário não possui cadastro de veterinário vinculado.';

  function navegar(passo: number) {
    const nova = new Date(data);

    if (visao === 'dia') nova.setDate(nova.getDate() + passo);
    else if (visao === 'semana') nova.setDate(nova.getDate() + passo * 7);
    else nova.setMonth(nova.getMonth() + passo);

    setData(nova);
  }

  /** HU-006: a equipe também confirma, cancela ou conclui a sessão pela agenda. */
  async function alterarStatus(sessaoId: string, status: string) {
    setAviso('');
    setErro('');

    try {
      await api.patch(`/api/sessoes/${sessaoId}/status`, { status });
      setAviso(`Sessão marcada como ${status.toLowerCase()}.`);
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível atualizar o status da sessão.'));
    }
  }

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

  /** Nas visões semana e mês as sessões são exibidas agrupadas por dia. */
  const porDia = useMemo(() => {
    const grupos = new Map<string, ItemAgenda[]>();

    for (const sessao of sessoes) {
      const chave = paraValorInputData(sessao.dataHora);
      grupos.set(chave, [...(grupos.get(chave) ?? []), sessao]);
    }

    return [...grupos.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [sessoes]);

  return (
    <>
      <CabecalhoPagina
        titulo="Minha agenda"
        descricao="Organize as sessões de fisioterapia dos seus pacientes."
        acoes={
          <>
            <button type="button" className="vc-botao-secundario" onClick={() => setModalBloqueios(true)}>
              <CalendarOff size={16} />
              Bloqueios
            </button>
            <button type="button" className="vc-botao-primario" onClick={() => setModalAgendar(true)}>
              <Plus size={16} />
              Agendar sessão
            </button>
          </>
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

            {/* HU-004, CA-3: alternância entre as visões Dia, Semana e Mês. */}
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
      </Card>

      {carregando ? (
        <Carregando texto="Carregando agenda..." />
      ) : sessoes.length === 0 ? (
        <Card>
          <SemDados
            icone={<CalendarDays size={40} />}
            titulo="Nenhuma sessão neste período"
            descricao="Não há atendimentos agendados para a data selecionada."
            acao={
              <button type="button" className="vc-botao-sutil" onClick={() => setModalAgendar(true)}>
                <Plus size={16} />
                Agendar uma sessão
              </button>
            }
          />
        </Card>
      ) : (
        <div className="space-y-6">
          {porDia.map(([dia, itens]) => (
            <Card key={dia}>
              {visao !== 'dia' && (
                <div className="border-b border-slate-200 px-5 py-3">
                  <h2 className="text-sm font-semibold capitalize text-slate-700">
                    {new Date(`${dia}T12:00:00`).toLocaleDateString('pt-BR', {
                      weekday: 'long',
                      day: '2-digit',
                      month: 'long',
                    })}
                  </h2>
                </div>
              )}

              <ul className="divide-y divide-slate-100">
                {itens.map((sessao) => (
                  <li key={sessao.sessaoId} className="flex flex-wrap items-center gap-4 px-5 py-4">
                    <div className="w-16 shrink-0 rounded-xl bg-brand-50 py-2 text-center">
                      <p className="text-sm font-bold text-brand-dark">{formatarHora(sessao.dataHora)}</p>
                    </div>

                    <div className="min-w-0 flex-1">
                      <Link
                        to={`/prontuario/${sessao.pacienteId}`}
                        className="font-semibold text-slate-900 hover:text-brand"
                      >
                        {sessao.nomePaciente}
                      </Link>
                      {sessao.nomeTutor && <p className="truncate text-xs text-slate-500">Tutor: {sessao.nomeTutor}</p>}
                      {sessao.observacoes && (
                        <p className="mt-1 truncate text-xs text-slate-400">{sessao.observacoes}</p>
                      )}
                    </div>

                    {/* HU-004, CA-4: badges diferenciam visualmente os status. */}
                    <Etiqueta className={estiloStatusSessao[sessao.status]}>{sessao.status}</Etiqueta>

                    <div className="flex items-center gap-1">
                      {sessao.status === 'Aguardando confirmação' && (
                        <button
                          type="button"
                          onClick={() => alterarStatus(sessao.sessaoId, 'Confirmada')}
                          className="rounded-lg p-2 text-sucesso hover:bg-sucesso-claro"
                          title="Confirmar sessão"
                        >
                          <Check size={16} />
                        </button>
                      )}

                      {sessao.status !== 'Cancelada' && sessao.status !== 'Concluída' && (
                        <>
                          {/* HU-008: o atendimento é registrado a partir da sessão. */}
                          <button
                            type="button"
                            onClick={() => setSessaoParaAtender(sessao)}
                            className="rounded-lg p-2 text-brand hover:bg-brand-100"
                            title="Registrar atendimento"
                          >
                            <ClipboardPlus size={16} />
                          </button>

                          <button
                            type="button"
                            onClick={() => alterarStatus(sessao.sessaoId, 'Cancelada')}
                            className="rounded-lg p-2 text-perigo hover:bg-perigo-claro"
                            title="Cancelar sessão"
                          >
                            <X size={16} />
                          </button>
                        </>
                      )}

                      {sessao.possuiAtendimento && (
                        <span className="ml-1 flex items-center gap-1 text-xs text-sucesso" title="Atendimento registrado">
                          <Stethoscope size={14} />
                        </span>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}

      <ModalAgendarSessao
        aberto={modalAgendar}
        veterinarioId={veterinarioId}
        aoFechar={() => setModalAgendar(false)}
        aoSalvar={() => {
          setModalAgendar(false);
          setAviso('Sessão agendada com sucesso.');
          recarregar();
        }}
      />

      <ModalBloqueiosAgenda
        aberto={modalBloqueios}
        veterinarioId={veterinarioId}
        aoFechar={() => setModalBloqueios(false)}
        aoSalvar={recarregar}
      />

      <ModalRegistrarAtendimento
        sessao={sessaoParaAtender}
        aoFechar={() => setSessaoParaAtender(null)}
        aoSalvar={() => {
          setSessaoParaAtender(null);
          setAviso('Atendimento registrado com sucesso.');
          recarregar();
        }}
      />
    </>
  );
}
