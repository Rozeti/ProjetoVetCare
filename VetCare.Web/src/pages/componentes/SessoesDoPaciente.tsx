import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Activity, ChevronRight, ClipboardPlus, ListFilter, Paperclip } from 'lucide-react';
import type { ItemLinhaTempo, Tratamento } from '../../types';
import { Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { estiloEscalaDor, formatarDataHora, paraValorInputData, tratamentoAtivo } from '../../utils/formato';

interface Props {
  pacienteId: string;
  /** Sessões registradas do animal (eventos "Sessão" da linha do tempo), da mais recente à mais antiga. */
  sessoes: ItemLinhaTempo[];
  tratamentos: Tratamento[];
  /** Abre o registro completo da sessão. */
  aoAbrir: (sessao: ItemLinhaTempo) => void;
  /** Registra uma sessão realizada; ausente para quem não registra ou com o prontuário encerrado. */
  aoRegistrar?: (tratamento: Tratamento) => void;
  /** Atalho para a tela de sessões, só para quem tem acesso a ela. */
  exibeLinkDaTela: boolean;
}

/**
 * Aba Sessões do prontuário: cada sessão de fisioterapia realizada, de todos os tratamentos do
 * animal, com filtro por tratamento, período e número da sessão. É daqui (ou do cartão do
 * tratamento) que uma nova sessão é registrada.
 */
export function SessoesDoPaciente({ pacienteId, sessoes, tratamentos, aoAbrir, aoRegistrar, exibeLinkDaTela }: Props) {
  const [tratamentoId, setTratamentoId] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [numero, setNumero] = useState('');
  const [escolhendoTratamento, setEscolhendoTratamento] = useState(false);

  const emCurso = tratamentos.filter((t) => tratamentoAtivo(t.status));
  const nomeDoTratamento = (id?: string | null) => tratamentos.find((t) => t.id === id)?.nome ?? '';

  // Só os tratamentos que já têm sessões entram no filtro.
  const comSessoes = tratamentos.filter((t) => sessoes.some((s) => s.tratamentoId === t.id));

  const filtrando = !!tratamentoId || !!de || !!ate || !!numero;
  const periodoInvalido = !!de && !!ate && ate < de;

  const visiveis = sessoes.filter((sessao) => {
    const dia = paraValorInputData(sessao.data);
    if (tratamentoId && sessao.tratamentoId !== tratamentoId) return false;
    if (de && dia < de) return false;
    if (ate && dia > ate) return false;
    if (numero && sessao.numeroSessao !== Number(numero)) return false;
    return true;
  });

  function registrar() {
    if (!aoRegistrar) return;

    // Com um só tratamento em curso, não há o que escolher.
    if (emCurso.length === 1) aoRegistrar(emCurso[0]);
    else setEscolhendoTratamento(true);
  }

  function limpar() {
    setTratamentoId('');
    setDe('');
    setAte('');
    setNumero('');
  }

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
        <div>
          <h2 className="font-semibold text-slate-900">Sessões</h2>
          <p className="text-xs text-slate-500">
            Cada sessão de fisioterapia realizada, com o que foi feito e como o animal reagiu.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {exibeLinkDaTela && sessoes.length > 0 && (
            <Link to={`/sessoes?pacienteId=${pacienteId}`} className="vc-botao-secundario">
              <ListFilter size={16} />
              Abrir na tela de sessões
            </Link>
          )}

          {aoRegistrar && emCurso.length > 0 && (
            <button type="button" className="vc-botao-primario" onClick={registrar}>
              <ClipboardPlus size={16} />
              Registrar sessão
            </button>
          )}
        </div>
      </div>

      {sessoes.length > 0 && (
        <div className="flex flex-wrap items-end gap-3 border-b border-slate-100 px-5 py-3">
          <label className="text-xs font-semibold text-slate-600">
            Tratamento
            <select
              className="vc-campo mt-1 block w-auto py-1.5"
              value={tratamentoId}
              onChange={(e) => setTratamentoId(e.target.value)}
            >
              <option value="">Todos</option>
              {comSessoes.map((tratamento) => (
                <option key={tratamento.id} value={tratamento.id}>
                  {tratamento.nome}
                </option>
              ))}
            </select>
          </label>
          <label className="text-xs font-semibold text-slate-600">
            De
            <input
              type="date"
              className="vc-campo mt-1 block w-auto py-1.5"
              value={de}
              max={ate || undefined}
              onChange={(e) => setDe(e.target.value)}
            />
          </label>
          <label className="text-xs font-semibold text-slate-600">
            Até
            <input
              type="date"
              className="vc-campo mt-1 block w-auto py-1.5"
              value={ate}
              min={de || undefined}
              onChange={(e) => setAte(e.target.value)}
            />
          </label>
          <label className="text-xs font-semibold text-slate-600">
            Sessão nº
            <input
              type="number"
              min={1}
              className="vc-campo mt-1 block w-24 py-1.5"
              value={numero}
              onChange={(e) => setNumero(e.target.value)}
            />
          </label>

          {filtrando && (
            <button type="button" onClick={limpar} className="pb-2.5 text-xs font-medium text-brand hover:underline">
              Limpar filtros
            </button>
          )}

          <p className="ml-auto pb-2.5 text-xs text-slate-500" aria-live="polite">
            {visiveis.length === sessoes.length
              ? `${sessoes.length} ${sessoes.length === 1 ? 'sessão' : 'sessões'}`
              : `${visiveis.length} de ${sessoes.length} sessões`}
          </p>
        </div>
      )}

      {periodoInvalido && <p className="px-5 pt-3 text-sm text-perigo">A data final não pode ser anterior à inicial.</p>}

      {sessoes.length === 0 ? (
        <SemDados
          icone={<Activity size={40} />}
          titulo="Nenhuma sessão registrada"
          descricao={
            emCurso.length > 0
              ? 'Ao fim de cada sessão de fisioterapia, use “Registrar sessão” para anotar o que foi feito.'
              : tratamentos.length === 0
                ? 'As sessões são registradas dentro de um tratamento, e o tratamento nasce de uma avaliação.'
                : 'Não há tratamento em andamento para registrar sessões.'
          }
        />
      ) : visiveis.length === 0 ? (
        <SemDados
          icone={<Activity size={40} />}
          titulo="Nenhuma sessão no filtro escolhido"
          descricao="Ajuste ou limpe os filtros para ver as outras sessões."
          compacto
        />
      ) : (
        <ol className="divide-y divide-slate-100">
          {visiveis.map((sessao) => (
            <li key={sessao.chave}>
              <button
                type="button"
                onClick={() => aoAbrir(sessao)}
                className="flex w-full items-start gap-3 px-5 py-3 text-left transition hover:bg-slate-50"
              >
                <span className="flex h-8 min-w-8 shrink-0 items-center justify-center rounded-full bg-cyan-100 px-1.5 text-xs font-bold text-cyan-800">
                  {sessao.numeroSessao}
                </span>

                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-slate-800">
                    {sessao.campos['Procedimentos realizados'] ?? sessao.descricao}
                  </span>
                  <span className="block truncate text-xs text-slate-500">
                    {[nomeDoTratamento(sessao.tratamentoId), formatarDataHora(sessao.data), sessao.autor]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                  {sessao.campos['Reação do animal'] && (
                    <span className="block truncate text-xs text-slate-500">
                      Reação: {sessao.campos['Reação do animal']}
                    </span>
                  )}
                </span>

                <span className="flex shrink-0 items-center gap-2">
                  {sessao.midias.length > 0 && (
                    <span className="inline-flex items-center gap-1 text-xs text-slate-500" title="Fotos e vídeos anexados">
                      <Paperclip size={13} />
                      {sessao.midias.length}
                    </span>
                  )}
                  {sessao.escalaDor != null && (
                    <Etiqueta className={estiloEscalaDor(sessao.escalaDor)}>Dor {sessao.escalaDor}/10</Etiqueta>
                  )}
                  <ChevronRight size={18} className="text-slate-300" aria-hidden="true" />
                </span>
              </button>
            </li>
          ))}
        </ol>
      )}

      {/* Mais de um tratamento em curso: a sessão precisa dizer de qual deles é. */}
      <Modal
        aberto={escolhendoTratamento}
        titulo="Registrar sessão"
        descricao="De qual tratamento é a sessão realizada?"
        aoFechar={() => setEscolhendoTratamento(false)}
      >
        <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
          {emCurso.map((tratamento) => (
            <li key={tratamento.id}>
              <button
                type="button"
                onClick={() => {
                  setEscolhendoTratamento(false);
                  aoRegistrar?.(tratamento);
                }}
                className="flex w-full items-center gap-3 px-4 py-3 text-left transition hover:bg-slate-50"
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold text-slate-800">{tratamento.nome}</span>
                  <span className="block text-xs text-slate-500">
                    {tratamento.evolucoesRegistradas} de {tratamento.sessoesPrevistas} sessões registradas · esta será a nº{' '}
                    {tratamento.evolucoesRegistradas + 1}
                  </span>
                </span>
                <ChevronRight size={18} className="text-slate-300" aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      </Modal>
    </Card>
  );
}
