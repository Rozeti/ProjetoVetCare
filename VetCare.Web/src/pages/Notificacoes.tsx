import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  ArrowRightLeft,
  Bell,
  CalendarClock,
  CalendarPlus,
  CheckCheck,
  ClipboardList,
  Loader2,
  MessageSquare,
  Settings2,
  Syringe,
} from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import { useConfirmacao } from '../hooks/useConfirmacao';
import type { Notificacao, TipoNotificacao } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, SemDados } from '../components/ui';
import { tempoRelativo } from '../utils/formato';

/** Ícone e cor por gatilho de notificação (HU-015). */
const ESTILO_POR_TIPO: Record<TipoNotificacao, { Icone: typeof Bell; cor: string; fundo: string }> = {
  SessaoAgendada: { Icone: CalendarPlus, cor: 'text-brand', fundo: 'bg-brand-100' },
  LembreteConfirmacao: { Icone: CalendarClock, cor: 'text-alerta', fundo: 'bg-alerta-claro' },
  StatusSessao: { Icone: CalendarClock, cor: 'text-info', fundo: 'bg-info-claro' },
  NovoRegistroProntuario: { Icone: ClipboardList, cor: 'text-sucesso', fundo: 'bg-sucesso-claro' },
  NovaMensagem: { Icone: MessageSquare, cor: 'text-brand', fundo: 'bg-brand-100' },
  DoseDeVacina: { Icone: Syringe, cor: 'text-alerta', fundo: 'bg-alerta-claro' },
  PacienteTransferido: { Icone: ArrowRightLeft, cor: 'text-info', fundo: 'bg-info-claro' },
};

const ESTILO_PADRAO = { Icone: Bell, cor: 'text-slate-500', fundo: 'bg-slate-100' };

/** Quantos avisos vêm por vez; "Ver mais" pede o próximo lote. */
const LOTE = 50;

/** HU-015: notificações dos eventos relevantes do tratamento. */
export function Notificacoes() {
  const navigate = useNavigate();
  const { ehTutor, usuario } = useAuth();
  const { confirmar } = useConfirmacao();

  const [apenasNovas, setApenasNovas] = useState(false);
  const [limite, setLimite] = useState(LOTE);
  const [marcandoTodas, setMarcandoTodas] = useState(false);
  const [aviso, setAviso] = useState('');

  const buscar = useCallback(async () => {
    const { data } = await api.get<Notificacao[]>('/api/notificacoes', {
      params: { apenasNaoVisualizadas: apenasNovas, limite },
    });

    return data;
  }, [apenasNovas, limite]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar as notificações.',
  );

  useAtualizacao(['notificacoes'], recarregar);

  const notificacoes = dados ?? [];

  async function marcarTodas() {
    if (marcandoTodas) return;

    // Não tem desfazer: o indicador do que ainda não foi lido some de uma vez.
    if (
      !(await confirmar({
        titulo: 'Marcar todas como lidas',
        mensagem: 'Marcar todas as notificações como lidas? Elas continuam na lista, sem o destaque de novas.',
        rotuloConfirmar: 'Marcar como lidas',
      }))
    ) {
      return;
    }

    setMarcandoTodas(true);
    setAviso('');

    try {
      await api.patch('/api/notificacoes/todas/visualizadas');
      setAviso('Todas as notificações foram marcadas como lidas.');
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível marcar as notificações.'));
    } finally {
      setMarcandoTodas(false);
    }
  }

  async function abrir(notificacao: Notificacao) {
    if (!notificacao.visualizada) {
      try {
        await api.patch(`/api/notificacoes/${notificacao.id}/visualizada`);
        recarregar();
      } catch {
        // Falhar ao marcar não deve impedir a navegação para o conteúdo; a próxima
        // leitura da lista mostra o aviso ainda como novo, que é o estado real.
      }
    }

    const destino = destinoDaNotificacao(notificacao.linkRelacionado, ehTutor);

    if (destino) {
      navigate(destino);
    }
  }

  const naoVisualizadas = notificacoes.filter((n) => !n.visualizada).length;
  const canais = [usuario?.notificarPorEmail && 'e-mail', usuario?.notificarPorPush && 'celular'].filter(Boolean);

  return (
    <>
      <CabecalhoPagina
        titulo="Notificações"
        descricao="Acompanhe o andamento do tratamento sem precisar verificar o sistema o tempo todo."
        acoes={
          <>
            <Link to="/perfil" className="vc-botao-secundario" title="Escolher por quais canais ser avisado">
              <Settings2 size={16} />
              {canais.length > 0 ? `Também por ${canais.join(' e ')}` : 'Só no sistema'}
            </Link>
            {naoVisualizadas > 0 && (
              <button type="button" className="vc-botao-secundario" onClick={marcarTodas} disabled={marcandoTodas}>
                {marcandoTodas ? <Loader2 className="animate-spin" size={16} /> : <CheckCheck size={16} />}
                Marcar todas como lidas
              </button>
            )}
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

      <Card className="mb-4 p-4">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            className="h-4 w-4 rounded accent-brand"
            checked={apenasNovas}
            onChange={(e) => setApenasNovas(e.target.checked)}
          />
          Mostrar apenas as não visualizadas
        </label>
      </Card>

      {carregando ? (
        <Carregando texto="Carregando notificações..." />
      ) : notificacoes.length === 0 ? (
        <Card>
          <SemDados
            icone={<Bell size={40} />}
            titulo={apenasNovas ? 'Nenhuma notificação nova' : 'Nenhuma notificação'}
            descricao="Avisos de sessões agendadas, lembretes de confirmação, vacinas a vencer, novos registros no prontuário e mensagens aparecem aqui."
          />
        </Card>
      ) : (
        <Card>
          <ul className="divide-y divide-slate-100">
            {notificacoes.map((notificacao) => {
              const estilo = ESTILO_POR_TIPO[notificacao.tipo] ?? ESTILO_PADRAO;
              const { Icone } = estilo;

              return (
                <li key={notificacao.id}>
                  <button
                    type="button"
                    onClick={() => abrir(notificacao)}
                    className={`flex w-full items-start gap-4 px-5 py-4 text-left transition hover:bg-slate-50 ${
                      notificacao.visualizada ? '' : 'bg-brand-50/40'
                    }`}
                  >
                    <div className={`shrink-0 rounded-xl p-2.5 ${estilo.fundo} ${estilo.cor}`}>
                      <Icone size={18} />
                    </div>

                    <div className="min-w-0 flex-1">
                      <div className="flex items-baseline justify-between gap-3">
                        <p className="font-semibold text-slate-900">{notificacao.titulo}</p>
                        <span className="shrink-0 text-xs text-slate-400">{tempoRelativo(notificacao.dataCriacao)}</span>
                      </div>
                      <p className="mt-0.5 text-sm text-slate-600">{notificacao.conteudo}</p>
                    </div>

                    {!notificacao.visualizada && (
                      <span className="mt-2 h-2.5 w-2.5 shrink-0 rounded-full bg-brand" role="img" aria-label="Não lida" />
                    )}
                  </button>
                </li>
              );
            })}
          </ul>

          {/* A API devolve um lote por vez; o botão pede o próximo até a lista acabar. */}
          {notificacoes.length >= limite && (
            <div className="border-t border-slate-100 px-5 py-3 text-center">
              <button type="button" className="vc-botao-sutil" onClick={() => setLimite((atual) => atual + LOTE)}>
                Ver notificações mais antigas
              </button>
            </div>
          )}
        </Card>
      )}
    </>
  );
}

/**
 * A API já envia o caminho certo para cada perfil; a tradução aqui é uma rede de
 * segurança para notificações antigas, gravadas antes dessa regra existir.
 */
function destinoDaNotificacao(link: string | null | undefined, ehTutor: boolean): string | null {
  if (!link) {
    return null;
  }

  if (ehTutor && link === '/agenda') {
    return '/minha-agenda';
  }

  return link;
}
