import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import {
  Activity,
  CalendarClock,
  CalendarDays,
  ClipboardList,
  MessageSquare,
  PawPrint,
} from 'lucide-react';
import { api } from '../services/api';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { useAuth } from '../contexts/auth';
import type { Indicadores } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Estatistica, Etiqueta, SemDados } from '../components/ui';
import { estiloStatusSessao, formatarDataExtensa, formatarHora } from '../utils/formato';

/** HU-016: painel com os indicadores operacionais do dia. */
export function Dashboard() {
  const { usuario, ehVeterinario } = useAuth();

  const buscar = useCallback(async () => {
    const { data } = await api.get<Indicadores>('/api/dashboard/indicadores');
    return data;
  }, []);

  const {
    dados: indicadores,
    carregando,
    erro,
    recarregar,
  } = useCarregamento(buscar, 'Não foi possível carregar os indicadores do dia.');

  // HU-016: os números do dia — inclusive o de confirmações pendentes — refletem o
  // que o tutor acabou de fazer no aplicativo.
  useAtualizacao(
    ['sessoes', 'atendimentos', 'avaliacoes', 'pets', 'mensagens', 'notificacoes'],
    recarregar,
  );

  const primeiroNome = usuario?.nome.split(' ')[0] ?? '';

  return (
    <>
      <CabecalhoPagina
        titulo={`Olá, ${primeiroNome}`}
        descricao={formatarDataExtensa(new Date())}
        acoes={
          <Link to={ehVeterinario ? '/agenda' : '/agenda-geral'} className="vc-botao-secundario">
            <CalendarDays size={16} />
            Ver agenda completa
          </Link>
        }
      />

      {erro && (
        <div className="mb-6">
          <Alerta tipo="erro">{erro}</Alerta>
        </div>
      )}

      {carregando ? (
        <Carregando texto="Carregando indicadores..." />
      ) : !indicadores ? null : (
        <>
          {/* RN-008: o escopo dos números muda conforme o perfil de quem consulta. */}
          <p className="mb-4 text-xs text-slate-500">
            {indicadores.escopo === 'Veterinario'
              ? 'Indicadores restritos aos seus pacientes e sessões.'
              : 'Indicadores consolidados de toda a clínica.'}
          </p>

          {/* HU-016, CA-1: avaliações, atendimentos, confirmações pendentes e pacientes ativos. */}
          <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Estatistica
              rotulo="Avaliações do dia"
              valor={indicadores.avaliacoesDoDia}
              icone={<ClipboardList size={20} />}
            />
            <Estatistica
              rotulo="Atendimentos do dia"
              valor={indicadores.atendimentosDoDia}
              icone={<Activity size={20} />}
              cor="text-info"
              fundo="bg-info-claro"
            />
            <Estatistica
              rotulo="Confirmações pendentes"
              valor={indicadores.confirmacoesPendentes}
              icone={<CalendarClock size={20} />}
              cor="text-alerta"
              fundo="bg-alerta-claro"
              destaque={`${indicadores.sessoesDoDia} sessão(ões) hoje`}
            />
            <Estatistica
              rotulo="Pacientes ativos"
              valor={indicadores.pacientesAtivos}
              icone={<PawPrint size={20} />}
              cor="text-sucesso"
              fundo="bg-sucesso-claro"
            />
          </div>

          <div className="grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
                <h2 className="font-semibold text-slate-900">Próximas sessões de hoje</h2>
                <Link to={ehVeterinario ? '/agenda' : '/agenda-geral'} className="text-sm font-medium text-brand hover:underline">
                  Ver todas
                </Link>
              </div>

              {/* HU-016, CA-3: dia sem registros aparece como informação, não como erro. */}
              {indicadores.proximasSessoes.length === 0 ? (
                <SemDados
                  icone={<CalendarDays size={40} />}
                  titulo="Nenhuma sessão para hoje"
                  descricao="Quando houver sessões agendadas para o dia, elas aparecem aqui."
                />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {indicadores.proximasSessoes.map((sessao) => (
                    <li key={sessao.sessaoId} className="flex items-center gap-4 px-5 py-4">
                      <div className="w-14 shrink-0 rounded-xl bg-slate-100 py-2 text-center">
                        <p className="text-sm font-bold text-slate-900">{formatarHora(sessao.dataHora)}</p>
                      </div>

                      <div className="min-w-0 flex-1">
                        <Link
                          to={`/prontuario/${sessao.pacienteId}`}
                          className="truncate font-semibold text-slate-900 hover:text-brand"
                        >
                          {sessao.nomePaciente}
                        </Link>
                        <p className="truncate text-xs text-slate-500">
                          {sessao.nomeTutor && `Tutor: ${sessao.nomeTutor}`}
                          {sessao.nomeVeterinario && ` · ${sessao.nomeVeterinario}`}
                        </p>
                      </div>

                      <Etiqueta className={estiloStatusSessao[sessao.status]}>{sessao.status}</Etiqueta>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <div className="space-y-4">
              <Card className="p-5">
                <h2 className="mb-3 font-semibold text-slate-900">Sua caixa de entrada</h2>

                <Link
                  to="/mensagens"
                  className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-3 transition hover:bg-slate-50"
                >
                  <span className="flex items-center gap-2 text-sm text-slate-700">
                    <MessageSquare size={16} className="text-brand" />
                    Mensagens não lidas
                  </span>
                  <span className="font-bold text-slate-900">{indicadores.mensagensNaoLidas}</span>
                </Link>

                <Link
                  to="/notificacoes"
                  className="mt-2 flex items-center justify-between rounded-xl border border-slate-200 px-4 py-3 transition hover:bg-slate-50"
                >
                  <span className="flex items-center gap-2 text-sm text-slate-700">
                    <CalendarClock size={16} className="text-alerta" />
                    Notificações novas
                  </span>
                  <span className="font-bold text-slate-900">{indicadores.notificacoesNaoVisualizadas}</span>
                </Link>
              </Card>

              <Card className="p-5">
                <h2 className="mb-3 font-semibold text-slate-900">Atalhos</h2>
                <div className="space-y-2">
                  <Link to="/pacientes" className="vc-botao-secundario w-full justify-start">
                    <PawPrint size={16} />
                    Pacientes
                  </Link>
                  <Link to="/tutores" className="vc-botao-secundario w-full justify-start">
                    <ClipboardList size={16} />
                    Tutores
                  </Link>
                  <Link to="/relatorios" className="vc-botao-secundario w-full justify-start">
                    <Activity size={16} />
                    Relatórios de produtividade
                  </Link>
                </div>
              </Card>
            </div>
          </div>
        </>
      )}
    </>
  );
}
