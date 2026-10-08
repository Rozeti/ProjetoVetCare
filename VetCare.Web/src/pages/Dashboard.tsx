import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import {
  Activity,
  CalendarClock,
  CalendarDays,
  ClipboardList,
  MessageSquare,
  PawPrint,
  Syringe,
} from 'lucide-react';
import { api } from '../services/api';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { useAuth } from '../contexts/auth';
import type { Indicadores, Vacina } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Estatistica, Etiqueta, SemDados } from '../components/ui';
import { estiloSituacaoDose, estiloStatusSessao, formatarData, formatarDataExtensa, formatarHora } from '../utils/formato';

/** Janela do painel de prevenção, igual à da classificação "A vencer" da carteira. */
const DIAS_DE_PREVENCAO = 30;

/** HU-016: painel com os indicadores operacionais do dia. */
export function Dashboard() {
  const { usuario, ehVeterinario, temPerfil } = useAuth();

  const buscar = useCallback(async () => {
    const [indicadores, prevencao] = await Promise.all([
      api.get<Indicadores>('/api/dashboard/indicadores'),
      // Doses vencidas ou a vencer dos pacientes do recorte de quem consulta (RN-008).
      api.get<Vacina[]>('/api/vacinas/vencendo', { params: { dias: DIAS_DE_PREVENCAO } }),
    ]);

    return { indicadores: indicadores.data, prevencao: prevencao.data };
  }, []);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar os indicadores do dia.',
  );

  // HU-016: os números do dia — inclusive o de confirmações pendentes — refletem o
  // que o tutor acabou de fazer no aplicativo; a prevenção acompanha a carteira.
  useAtualizacao(
    ['sessoes', 'atendimentos', 'avaliacoes', 'pets', 'mensagens', 'notificacoes', 'vacinas'],
    recarregar,
  );

  const indicadores = dados?.indicadores;
  const prevencao = dados?.prevencao ?? [];
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
          <Alerta tipo="erro" aoFechar={() => setErro('')}>
            {erro}
          </Alerta>
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
              rotulo={indicadores.escopo === 'Veterinario' ? 'Meus pacientes ativos' : 'Pacientes ativos'}
              valor={indicadores.pacientesAtivos}
              icone={<PawPrint size={20} />}
              cor="text-sucesso"
              fundo="bg-sucesso-claro"
            />
          </div>

          <div className="grid gap-6 lg:grid-cols-3">
            <div className="space-y-6 lg:col-span-2">
              <Card>
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

              {/* Painel de prevenção: doses vencidas ou a vencer nos próximos dias, para a clínica agir antes. */}
              <Card>
                <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
                  <h2 className="flex items-center gap-2 font-semibold text-slate-900">
                    <Syringe size={18} className="text-alerta" />
                    Prevenção a vencer
                  </h2>
                  <span className="text-xs text-slate-500">
                    {prevencao.length} {prevencao.length === 1 ? 'dose' : 'doses'} nos próximos {DIAS_DE_PREVENCAO} dias
                  </span>
                </div>

                {prevencao.length === 0 ? (
                  <SemDados
                    icone={<Syringe size={40} />}
                    titulo="Nenhuma dose vencida ou a vencer"
                    descricao={`Vacinas, vermífugos e antipulgas com próxima dose nos próximos ${DIAS_DE_PREVENCAO} dias aparecem aqui.`}
                    compacto
                  />
                ) : (
                  <ul className="divide-y divide-slate-100">
                    {prevencao.map((dose) => (
                      <li key={dose.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
                        <div className="min-w-0 flex-1">
                          <Link
                            to={`/prontuario/${dose.pacienteId}?aba=vacinas`}
                            className="font-semibold text-slate-900 hover:text-brand"
                          >
                            {dose.nomePaciente}
                          </Link>
                          <p className="truncate text-xs text-slate-500">
                            {dose.nome}
                            {dose.descricaoDose && ` (${dose.descricaoDose.toLowerCase()})`}
                            {dose.nomeTutor && ` · Tutor: ${dose.nomeTutor}`}
                          </p>
                        </div>

                        <div className="text-right text-xs text-slate-500">
                          {dose.proximaDose && <p>{formatarData(dose.proximaDose)}</p>}
                          {dose.diasParaProximaDose != null && (
                            <p>
                              {dose.diasParaProximaDose < 0
                                ? `${Math.abs(dose.diasParaProximaDose)} dia(s) em atraso`
                                : dose.diasParaProximaDose === 0
                                  ? 'vence hoje'
                                  : `em ${dose.diasParaProximaDose} dia(s)`}
                            </p>
                          )}
                        </div>

                        <Etiqueta className={estiloSituacaoDose[dose.situacaoDose]}>{dose.situacaoDose}</Etiqueta>
                      </li>
                    ))}
                  </ul>
                )}
              </Card>
            </div>

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
                  {/* RN-008: o apoio administrativo não tem acesso aos relatórios. */}
                  {temPerfil('Administrador', 'Veterinario') && (
                    <Link to="/relatorios" className="vc-botao-secundario w-full justify-start">
                      <Activity size={16} />
                      Relatórios de produtividade
                    </Link>
                  )}
                </div>
              </Card>
            </div>
          </div>
        </>
      )}
    </>
  );
}
