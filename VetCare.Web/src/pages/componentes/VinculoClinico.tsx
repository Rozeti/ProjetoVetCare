import { ClipboardList, HelpCircle, Stethoscope } from 'lucide-react';
import type { Avaliacao, Tratamento } from '../../types';
import { Campo } from '../../components/ui';
import { formatarData, resumirTexto } from '../../utils/formato';
import { descreverAvaliacao, opcoesDeOrigem, type ComOrigem } from '../../utils/vinculoClinico';
import { Atalho } from './RegistroClinico';

/** Campo de origem: as avaliações do animal e os tratamentos planejados ou em andamento. */
export function CampoOrigem({
  valor,
  aoMudar,
  avaliacoes,
  tratamentos,
  dica,
}: {
  valor: string;
  aoMudar: (valor: string) => void;
  avaliacoes: Avaliacao[];
  /** Já filtrados por tratamentosAtivos. */
  tratamentos: Tratamento[];
  dica: string;
}) {
  return (
    <Campo rotulo="Origem" obrigatorio dica={dica}>
      <select className="vc-campo" value={valor} onChange={(e) => aoMudar(e.target.value)}>
        <option value="">Selecione a avaliação ou o tratamento</option>
        {avaliacoes.length > 0 && (
          <optgroup label="Avaliações">
            {avaliacoes.map((avaliacao) => (
              <option key={avaliacao.id} value={`avaliacao:${avaliacao.id}`}>
                {descreverAvaliacao(avaliacao)}
              </option>
            ))}
          </optgroup>
        )}
        {tratamentos.length > 0 && (
          <optgroup label="Tratamentos">
            {tratamentos.map((tratamento) => (
              <option key={tratamento.id} value={`tratamento:${tratamento.id}`}>
                {tratamento.nome} ({tratamento.status.toLowerCase()})
              </option>
            ))}
          </optgroup>
        )}
      </select>
    </Campo>
  );
}

/** Origem já definida pelo botão que abriu o formulário, mostrada como contexto. */
export function ResumoDaOrigem({ origem }: { origem: string }) {
  const doTratamento = origem.startsWith('Tratamento');

  return (
    <div className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
      <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-brand-50 text-brand" aria-hidden="true">
        {doTratamento ? <Stethoscope size={15} /> : <ClipboardList size={15} />}
      </span>
      <span className="min-w-0">
        <span className="block text-xs font-semibold uppercase tracking-wide text-slate-500">Origem</span>
        <strong className="block text-slate-800">{origem || 'Não encontrada'}</strong>
      </span>
    </div>
  );
}

/** Filtro das abas Medicação e Exames; só aparece quando a lista mistura origens. */
export function FiltroPorOrigem({
  itens,
  valor,
  aoMudar,
}: {
  itens: ComOrigem[];
  valor: string;
  aoMudar: (valor: string) => void;
}) {
  const opcoes = opcoesDeOrigem(itens);

  if (opcoes.length < 2) return null;

  const grupos = (['Avaliações', 'Tratamentos', 'Outros'] as const)
    .map((grupo) => ({ grupo, opcoes: opcoes.filter((o) => o.grupo === grupo) }))
    .filter((g) => g.opcoes.length > 0);

  return (
    <select
      className="vc-campo w-auto max-w-full py-2 sm:max-w-xs"
      value={opcoes.some((opcao) => opcao.valor === valor) ? valor : ''}
      onChange={(e) => aoMudar(e.target.value)}
      aria-label="Filtrar pela origem"
    >
      <option value="">Todas as origens</option>
      {grupos.map(({ grupo, opcoes: doGrupo }) => (
        <optgroup key={grupo} label={grupo}>
          {doGrupo.map((opcao) => (
            <option key={opcao.valor} value={opcao.valor}>
              {opcao.rotulo}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  );
}

/** Texto curto da origem, para a linha resumida da receita ou do pedido. */
export function LinhaDaOrigem({ item }: { item: ComOrigem }) {
  if (item.tipoOrigem === 'Avaliação') {
    return (
      <span className="inline-flex max-w-full items-center gap-1.5 text-slate-600">
        <ClipboardList size={13} className="shrink-0 text-slate-400" aria-hidden="true" />
        <span className="truncate">
          <span className="font-medium">Avaliação</span>
          {item.dataAvaliacao && ` de ${formatarData(item.dataAvaliacao)}`}
          {item.motivoAvaliacao && ` — ${item.motivoAvaliacao}`}
        </span>
      </span>
    );
  }

  if (item.tipoOrigem === 'Tratamento') {
    return (
      <span className="inline-flex max-w-full items-center gap-1.5 text-slate-600">
        <Stethoscope size={13} className="shrink-0 text-slate-400" aria-hidden="true" />
        <span className="truncate">
          <span className="font-medium">Tratamento</span> {item.nomeTratamento}
        </span>
      </span>
    );
  }

  return <span className="text-slate-400">Origem não registrada</span>;
}

/**
 * Origem da receita ou do pedido, com atalho para ela: a avaliação em que foi feito ou o
 * tratamento (também quando foi feito em uma sessão dele). Registros antigos da avaliação que
 * foram ligados a um tratamento mostram os dois.
 */
export function OrigemDoDocumento({
  item,
  aoVerAvaliacao,
  aoVerTratamento,
}: {
  item: ComOrigem;
  aoVerAvaliacao?: (avaliacaoId: string) => void;
  aoVerTratamento?: (tratamentoId: string) => void;
}) {
  const { avaliacaoId, tratamentoId } = item;

  const tratamento = tratamentoId && (
    <Atalho
      icone={<Stethoscope size={13} className="shrink-0 text-brand" />}
      aoClicar={aoVerTratamento ? () => aoVerTratamento(tratamentoId) : undefined}
      titulo="Abrir o tratamento"
    >
      {item.tipoOrigem === 'Avaliação' ? 'Ligada ao tratamento' : 'Tratamento'} {item.nomeTratamento}
    </Atalho>
  );

  if (item.tipoOrigem === 'Avaliação' && avaliacaoId) {
    return (
      <div className="flex flex-wrap gap-2">
        <Atalho
          icone={<ClipboardList size={13} className="shrink-0 text-brand" />}
          aoClicar={aoVerAvaliacao ? () => aoVerAvaliacao(avaliacaoId) : undefined}
          titulo="Abrir a avaliação"
        >
          Avaliação{item.dataAvaliacao && ` de ${formatarData(item.dataAvaliacao)}`}
          {item.motivoAvaliacao && ` — ${resumirTexto(item.motivoAvaliacao, 60)}`}
        </Atalho>
        {tratamento}
      </div>
    );
  }

  if (item.tipoOrigem === 'Tratamento' && tratamento) {
    return <div className="flex flex-wrap gap-2">{tratamento}</div>;
  }

  return (
    <p className="inline-flex items-center gap-1.5 text-sm text-slate-500">
      <HelpCircle size={14} aria-hidden="true" />
      Registro anterior à origem obrigatória: sem avaliação ou tratamento registrado.
    </p>
  );
}
