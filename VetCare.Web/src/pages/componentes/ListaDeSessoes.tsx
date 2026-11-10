import { useState, type ReactNode } from 'react';
import { CalendarClock, ClipboardPlus, FileText, Power, RotateCcw } from 'lucide-react';
import type { Sessao } from '../../types';
import { Etiqueta } from '../../components/ui';
import { estiloSituacaoSessao, formatarIntervalo, paraValorInputData } from '../../utils/formato';

export interface AcoesDaSessao {
  /** Registra o atendimento de uma sessão ainda sem registro (Administrador e Veterinário). */
  aoRegistrar?: (sessao: Sessao) => void;
  /** Abre o registro do atendimento já feito. */
  aoAbrirRegistro?: (sessao: Sessao) => void;
  /** Retira a sessão do plano; ausente para quem não pode. */
  aoInativar?: (sessao: Sessao) => void;
  aoReativar?: (sessao: Sessao) => void;
  /** O veterinário só inativa e reativa as sessões da própria agenda. */
  podeAlterar?: (sessao: Sessao) => boolean;
}

interface Props extends AcoesDaSessao {
  sessoes: Sessao[];
  /** Mostra o tratamento de cada sessão (na lista de todos os tratamentos do animal). */
  exibirTratamento?: boolean;
  vazio: string;
}

/** Quantas sessões de cada bloco aparecem antes de "mostrar mais". */
const VISIVEIS_PROXIMAS = 3;
const VISIVEIS_ANTERIORES = 5;

function dataExtensa(valor: string): string {
  return new Date(valor).toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' });
}

/**
 * Sessões cadastradas em três blocos, para a do dia estar sempre à mão: **Hoje** no topo,
 * depois as **Próximas** (a mais próxima primeiro) e as **Anteriores**, da mais recente para a
 * mais antiga — as antigas ficam no fim. Blocos longos mostram as primeiras e recolhem o resto.
 * Cada linha leva às ações possíveis: registrar o atendimento, abrir o registro, inativar ou
 * reativar. As inativadas ficam recolhidas, a um clique.
 */
export function ListaDeSessoes({ sessoes, exibirTratamento = false, vazio, ...acoes }: Props) {
  const [verInativadas, setVerInativadas] = useState(false);
  const [todasProximas, setTodasProximas] = useState(false);
  const [todasAnteriores, setTodasAnteriores] = useState(false);

  const inativadas = sessoes.filter((s) => s.inativa).length;
  const visiveis = sessoes.filter((s) => verInativadas || !s.inativa);

  const hoje = paraValorInputData(new Date());
  const diaDa = (sessao: Sessao) => paraValorInputData(sessao.dataHora);
  const crescente = (a: Sessao, b: Sessao) => a.dataHora.localeCompare(b.dataHora);

  // Realizada ou cancelada já não está por vir, mesmo com data à frente: vai para o histórico.
  const encerrada = (sessao: Sessao) => sessao.situacao === 'Realizada' || sessao.situacao === 'Cancelada';

  const doDia = visiveis.filter((s) => diaDa(s) === hoje).sort(crescente);
  const proximas = visiveis.filter((s) => diaDa(s) > hoje && !encerrada(s)).sort(crescente);
  const anteriores = visiveis
    .filter((s) => diaDa(s) < hoje || (diaDa(s) > hoje && encerrada(s)))
    .sort((a, b) => crescente(b, a));

  return (
    <div className="space-y-4">
      {visiveis.length === 0 && <p className="rounded-xl bg-slate-50 px-4 py-4 text-center text-sm text-slate-500">{vazio}</p>}

      {doDia.length > 0 && (
        <Bloco titulo="Hoje" destaque>
          {doDia.map((sessao) => (
            <LinhaDaSessao key={sessao.id} sessao={sessao} exibirTratamento={exibirTratamento} {...acoes} />
          ))}
        </Bloco>
      )}

      {proximas.length > 0 && (
        <Bloco
          titulo="Próximas"
          total={proximas.length}
          recolhidas={todasProximas ? 0 : proximas.length - VISIVEIS_PROXIMAS}
          aoAlternar={() => setTodasProximas((atual) => !atual)}
          expandido={todasProximas}
          rotulos={['próxima sessão', 'próximas sessões']}
        >
          {(todasProximas ? proximas : proximas.slice(0, VISIVEIS_PROXIMAS)).map((sessao) => (
            <LinhaDaSessao key={sessao.id} sessao={sessao} exibirTratamento={exibirTratamento} {...acoes} />
          ))}
        </Bloco>
      )}

      {anteriores.length > 0 && (
        <Bloco
          titulo="Anteriores"
          total={anteriores.length}
          recolhidas={todasAnteriores ? 0 : anteriores.length - VISIVEIS_ANTERIORES}
          aoAlternar={() => setTodasAnteriores((atual) => !atual)}
          expandido={todasAnteriores}
          rotulos={['sessão anterior', 'sessões anteriores']}
        >
          {(todasAnteriores ? anteriores : anteriores.slice(0, VISIVEIS_ANTERIORES)).map((sessao) => (
            <LinhaDaSessao key={sessao.id} sessao={sessao} exibirTratamento={exibirTratamento} {...acoes} />
          ))}
        </Bloco>
      )}

      {inativadas > 0 && (
        <button
          type="button"
          onClick={() => setVerInativadas((atual) => !atual)}
          className="text-xs font-medium text-slate-500 hover:text-slate-700 hover:underline"
          aria-pressed={verInativadas}
        >
          {verInativadas
            ? 'Ocultar as sessões inativadas'
            : `Mostrar ${inativadas === 1 ? 'a sessão inativada' : `as ${inativadas} sessões inativadas`}`}
        </button>
      )}
    </div>
  );
}

function Bloco({
  titulo,
  total,
  destaque = false,
  recolhidas = 0,
  expandido = false,
  rotulos = ['', ''],
  aoAlternar,
  children,
}: {
  titulo: string;
  total?: number;
  destaque?: boolean;
  recolhidas?: number;
  expandido?: boolean;
  /** Singular e plural do que fica recolhido: "próxima sessão" / "próximas sessões". */
  rotulos?: [string, string];
  aoAlternar?: () => void;
  children: ReactNode;
}) {
  return (
    <section>
      <h5 className={`mb-1.5 text-xs font-semibold uppercase tracking-wide ${destaque ? 'text-brand' : 'text-slate-500'}`}>
        {titulo}
        {total != null && <span className="ml-1 font-normal normal-case text-slate-400">({total})</span>}
      </h5>
      <ol className={`divide-y divide-slate-100 rounded-xl border ${destaque ? 'border-brand/40' : 'border-slate-200'}`}>{children}</ol>
      {aoAlternar && (recolhidas > 0 || expandido) && (
        <button type="button" onClick={aoAlternar} className="mt-1.5 text-xs font-medium text-brand hover:underline">
          {expandido ? 'Mostrar menos' : `Mostrar mais ${recolhidas} ${recolhidas === 1 ? rotulos[0] : rotulos[1]}`}
        </button>
      )}
    </section>
  );
}

function LinhaDaSessao({
  sessao,
  exibirTratamento,
  aoRegistrar,
  aoAbrirRegistro,
  aoInativar,
  aoReativar,
  podeAlterar = () => true,
}: AcoesDaSessao & { sessao: Sessao; exibirTratamento: boolean }) {
  const pendente = sessao.situacao === 'Agendada' || sessao.situacao === 'Aguardando registro';
  const alteravel = podeAlterar(sessao);

  return (
    <li className={`flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3 ${sessao.inativa ? 'bg-slate-50/70' : ''}`}>
      <span
        className="flex h-8 min-w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 px-1.5 text-xs font-bold text-slate-600"
        title={sessao.numeroSessao ? `Sessão nº ${sessao.numeroSessao} do tratamento` : 'Sessão ainda sem atendimento registrado'}
      >
        {sessao.numeroSessao ?? <CalendarClock size={14} aria-hidden="true" />}
      </span>

      {/* Em tela estreita o texto fica com a linha toda e a situação e os botões descem. */}
      <span className="min-w-48 flex-1">
        <span className={`block text-sm font-medium ${sessao.inativa ? 'text-slate-500 line-through' : 'text-slate-800'}`}>
          <span className="capitalize">{dataExtensa(sessao.dataHora)}</span> · {formatarIntervalo(sessao.dataHora, sessao.dataHoraFim)}
        </span>
        <span className="block truncate text-xs text-slate-500">
          {[exibirTratamento && sessao.nomeTratamento, sessao.nomeVeterinario, sessao.numeroSessao && sessao.tecnicasAplicadas]
            .filter(Boolean)
            .join(' · ')}
          {sessao.escalaDor != null && ` · dor ${sessao.escalaDor}/10`}
        </span>
      </span>

      <Etiqueta className={estiloSituacaoSessao[sessao.situacao]}>{sessao.situacao}</Etiqueta>

      <span className="flex items-center gap-1">
        {pendente && aoRegistrar && (
          <button
            type="button"
            onClick={() => aoRegistrar(sessao)}
            className={
              sessao.situacao === 'Aguardando registro' ? 'vc-botao-primario px-3 py-1.5 text-sm' : 'vc-botao-secundario px-3 py-1.5 text-sm'
            }
          >
            <ClipboardPlus size={15} />
            Registrar atendimento
          </button>
        )}

        {sessao.possuiAtendimento && aoAbrirRegistro && (
          <button type="button" onClick={() => aoAbrirRegistro(sessao)} className="vc-botao-secundario px-3 py-1.5 text-sm">
            <FileText size={15} />
            Ver registro
          </button>
        )}

        {!sessao.inativa && !sessao.possuiAtendimento && sessao.status !== 'Concluída' && aoInativar && alteravel && (
          <button
            type="button"
            onClick={() => aoInativar(sessao)}
            className="rounded-lg p-2 text-slate-400 hover:bg-perigo-claro hover:text-perigo"
            title="Inativar sessão"
            aria-label={`Inativar a sessão de ${dataExtensa(sessao.dataHora)}`}
          >
            <Power size={15} />
          </button>
        )}

        {sessao.inativa && aoReativar && alteravel && (
          <button
            type="button"
            onClick={() => aoReativar(sessao)}
            className="inline-flex items-center gap-1 rounded-lg px-2 py-1.5 text-sm font-medium text-slate-600 hover:bg-slate-100"
            title="Reativar sessão"
          >
            <RotateCcw size={14} />
            Reativar
          </button>
        )}
      </span>
    </li>
  );
}
