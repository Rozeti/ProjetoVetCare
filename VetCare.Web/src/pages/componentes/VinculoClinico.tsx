import type { Avaliacao, Tratamento } from '../../types';
import { Campo } from '../../components/ui';
import { formatarData, resumirTexto } from '../../utils/formato';
import { descreverSessao, opcoesDeTratamento, type ComTratamento, type SessaoRegistrada } from '../../utils/vinculoClinico';

/** Campo de origem com as avaliações e as sessões registradas do animal. */
export function CampoOrigem({
  valor,
  aoMudar,
  avaliacoes,
  sessoes,
  dica,
}: {
  valor: string;
  aoMudar: (valor: string) => void;
  avaliacoes: Avaliacao[];
  sessoes: SessaoRegistrada[];
  dica: string;
}) {
  return (
    <Campo rotulo="Origem" obrigatorio dica={dica}>
      <select className="vc-campo" value={valor} onChange={(e) => aoMudar(e.target.value)}>
        <option value="">Selecione a origem</option>
        {avaliacoes.length > 0 && (
          <optgroup label="Avaliações">
            {avaliacoes.map((avaliacao) => (
              <option key={avaliacao.id} value={`avaliacao:${avaliacao.id}`}>
                Avaliação de {formatarData(avaliacao.dataRegistro)} — {resumirTexto(avaliacao.motivoConsulta, 60)}
              </option>
            ))}
          </optgroup>
        )}
        {sessoes.length > 0 && (
          <optgroup label="Sessões registradas">
            {sessoes.map((sessao) => (
              <option key={sessao.id} value={`sessao:${sessao.id}`}>
                {descreverSessao(sessao)} — {sessao.nomeTratamento}
              </option>
            ))}
          </optgroup>
        )}
      </select>
    </Campo>
  );
}

/** Tratamento opcional de um registro que nasce de uma avaliação. */
export function CampoTratamento({
  tratamentos,
  valor,
  aoMudar,
  tipoDoRegistro,
}: {
  /** Já filtrados por tratamentosParaVinculo. */
  tratamentos: Tratamento[];
  valor: string;
  aoMudar: (id: string) => void;
  tipoDoRegistro: 'a receita' | 'o pedido';
}) {
  return (
    <Campo
      rotulo="Tratamento"
      dica={
        tratamentos.length === 0
          ? 'O animal não tem tratamento planejado ou em andamento.'
          : `Opcional: o tratamento para o qual ${tipoDoRegistro} é feito.`
      }
    >
      <select className="vc-campo" value={valor} onChange={(e) => aoMudar(e.target.value)}>
        <option value="">Sem tratamento vinculado</option>
        {tratamentos.map((tratamento) => (
          <option key={tratamento.id} value={tratamento.id}>
            {tratamento.nome} ({tratamento.status.toLowerCase()})
          </option>
        ))}
      </select>
    </Campo>
  );
}

/** Origem e tratamento já definidos, mostrados como contexto do formulário. */
export function ResumoDaOrigem({ origem, tratamento }: { origem: string; tratamento?: string }) {
  return (
    <div className="flex flex-wrap gap-x-4 gap-y-1 rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
      <span>
        Origem: <strong className="text-slate-800">{origem}</strong>
      </span>
      {tratamento && (
        <span>
          Tratamento: <strong className="text-slate-800">{tratamento}</strong>
        </span>
      )}
    </div>
  );
}

/** Filtro das abas Medicação e Exames; só aparece quando a lista mistura tratamentos. */
export function FiltroPorTratamento({
  itens,
  valor,
  aoMudar,
}: {
  itens: ComTratamento[];
  valor: string;
  aoMudar: (valor: string) => void;
}) {
  const opcoes = opcoesDeTratamento(itens);

  if (opcoes.length < 2) return null;

  return (
    <select
      className="vc-campo w-auto max-w-full py-2 sm:max-w-xs"
      value={opcoes.some((opcao) => opcao.valor === valor) ? valor : ''}
      onChange={(e) => aoMudar(e.target.value)}
      aria-label="Filtrar por tratamento"
    >
      <option value="">Todos os tratamentos</option>
      {opcoes.map((opcao) => (
        <option key={opcao.valor} value={opcao.valor}>
          {opcao.rotulo}
        </option>
      ))}
    </select>
  );
}

/** "Tratamento: Hidroterapia" — o nome leva ao tratamento no prontuário, quando há para onde ir. */
export function ReferenciaAoTratamento({
  item,
  aoVerTratamento,
}: {
  item: ComTratamento;
  aoVerTratamento?: (tratamentoId: string) => void;
}) {
  if (!item.tratamentoId) {
    return <span className="text-slate-400">Sem tratamento vinculado</span>;
  }

  const tratamentoId = item.tratamentoId;
  const nome = item.nomeTratamento || 'Tratamento';

  return (
    <span>
      Tratamento:{' '}
      {aoVerTratamento ? (
        <button
          type="button"
          className="font-medium text-brand hover:underline"
          onClick={() => aoVerTratamento(tratamentoId)}
          title="Abrir o tratamento"
        >
          {nome}
        </button>
      ) : (
        <strong className="font-medium text-slate-700">{nome}</strong>
      )}
    </span>
  );
}
