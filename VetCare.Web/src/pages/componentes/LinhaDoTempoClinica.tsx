import { useState, type ReactNode } from 'react';
import {
  Activity,
  ChevronRight,
  ClipboardList,
  FlaskConical,
  Lock,
  Pencil,
  Pill,
  Scissors,
  Stethoscope,
  Syringe,
  type LucideIcon,
} from 'lucide-react';
import { urlDoArquivo } from '../../services/api';
import type { CategoriaLinhaTempo, ItemLinhaTempo } from '../../types';
import { Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import {
  diaDoEvento,
  estiloEscalaDor,
  formatarData,
  formatarDataDoEvento,
  formatarDataHora,
  formatarDia,
  formatarHora,
  formatarPeso,
} from '../../utils/formato';

/** Ícone e cor de cada categoria de evento, os mesmos na lista e no registro completo. */
const CATEGORIAS: Record<CategoriaLinhaTempo, { Icone: LucideIcon; estilo: string; marcador: string }> = {
  Avaliação: { Icone: ClipboardList, estilo: 'bg-brand-100 text-brand-dark', marcador: 'bg-brand' },
  Tratamento: { Icone: Stethoscope, estilo: 'bg-info-claro text-violet-800', marcador: 'bg-info' },
  Sessão: { Icone: Activity, estilo: 'bg-cyan-100 text-cyan-800', marcador: 'bg-cyan-600' },
  Cirurgia: { Icone: Scissors, estilo: 'bg-perigo-claro text-red-800', marcador: 'bg-perigo' },
  Vacinação: { Icone: Syringe, estilo: 'bg-sucesso-claro text-emerald-800', marcador: 'bg-sucesso' },
  Receita: { Icone: Pill, estilo: 'bg-alerta-claro text-amber-800', marcador: 'bg-alerta' },
  Exame: { Icone: FlaskConical, estilo: 'bg-indigo-100 text-indigo-800', marcador: 'bg-indigo-600' },
  Observação: { Icone: Lock, estilo: 'bg-slate-100 text-slate-700', marcador: 'bg-slate-500' },
};

const ORDEM_DAS_CATEGORIAS = Object.keys(CATEGORIAS) as CategoriaLinhaTempo[];

export function EtiquetaCategoria({ categoria }: { categoria: CategoriaLinhaTempo }) {
  const { Icone, estilo } = CATEGORIAS[categoria] ?? CATEGORIAS.Observação;

  return (
    <Etiqueta className={estilo}>
      <Icone size={11} />
      {categoria}
    </Etiqueta>
  );
}

interface LinhaProps {
  itens: ItemLinhaTempo[];
  /** Abre o registro completo do evento. */
  aoSelecionar: (item: ItemLinhaTempo) => void;
}

/**
 * HU-011, CA-1: linha do tempo clínica do animal. Cada evento mostra em destaque a data em que
 * foi feito, com categoria, descrição resumida e responsável; selecionado, abre o registro
 * completo. O período (de/até) e as categorias filtram a lista — as sessões, por exemplo,
 * podem ficar de fora para uma leitura geral.
 */
export function LinhaDoTempo({ itens, aoSelecionar }: LinhaProps) {
  const [ocultas, setOcultas] = useState<Set<CategoriaLinhaTempo>>(new Set());
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');

  if (itens.length === 0) {
    return (
      <Card>
        <SemDados
          icone={<ClipboardList size={40} />}
          titulo="Prontuário ainda sem registros"
          descricao="Avaliações, tratamentos, sessões, vacinas, receitas e exames aparecem aqui em ordem cronológica assim que forem registrados."
        />
      </Card>
    );
  }

  const filtrandoPeriodo = !!de || !!ate;
  const periodoInvalido = !!de && !!ate && ate < de;

  const noPeriodo = itens.filter((item) => {
    const dia = diaDoEvento(item);
    return (!de || dia >= de) && (!ate || dia <= ate);
  });

  // As categorias continuam todas à vista; o número de cada uma acompanha o período.
  const contagem = new Map<CategoriaLinhaTempo, number>();
  for (const item of noPeriodo) contagem.set(item.categoria, (contagem.get(item.categoria) ?? 0) + 1);

  const presentes = ORDEM_DAS_CATEGORIAS.filter((categoria) => itens.some((item) => item.categoria === categoria));
  const visiveis = noPeriodo.filter((item) => !ocultas.has(item.categoria));

  function alternar(categoria: CategoriaLinhaTempo) {
    setOcultas((atual) => {
      const nova = new Set(atual);
      if (nova.has(categoria)) nova.delete(categoria);
      else nova.add(categoria);
      return nova;
    });
  }

  return (
    <>
      <div className="mb-3 flex flex-wrap items-end gap-3" role="group" aria-label="Período exibido na linha do tempo">
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

        {filtrandoPeriodo && (
          <button
            type="button"
            onClick={() => {
              setDe('');
              setAte('');
            }}
            className="pb-2.5 text-xs font-medium text-brand hover:underline"
          >
            Limpar datas
          </button>
        )}

        <p className="ml-auto pb-2.5 text-xs text-slate-500" aria-live="polite">
          {visiveis.length === itens.length
            ? `${itens.length} ${itens.length === 1 ? 'evento' : 'eventos'}`
            : `${visiveis.length} de ${itens.length} eventos`}
        </p>
      </div>

      {periodoInvalido && <p className="mb-3 text-sm text-perigo">A data final não pode ser anterior à inicial.</p>}

      <div className="mb-4 flex flex-wrap items-center gap-2" role="group" aria-label="Categorias exibidas na linha do tempo">
        {presentes.map((categoria) => {
          const { Icone, estilo } = CATEGORIAS[categoria];
          const ativa = !ocultas.has(categoria);

          return (
            <button
              key={categoria}
              type="button"
              aria-pressed={ativa}
              onClick={() => alternar(categoria)}
              className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold transition ${
                ativa ? estilo : 'bg-white text-slate-400 ring-1 ring-slate-200 hover:text-slate-600'
              }`}
            >
              <Icone size={13} />
              {categoria}
              <span className="opacity-70">{contagem.get(categoria) ?? 0}</span>
            </button>
          );
        })}

        {ocultas.size > 0 && (
          <button
            type="button"
            onClick={() => setOcultas(new Set())}
            className="text-xs font-medium text-brand hover:underline"
          >
            Mostrar todas
          </button>
        )}
      </div>

      {visiveis.length === 0 ? (
        <Card>
          <SemDados
            icone={<ClipboardList size={40} />}
            titulo={filtrandoPeriodo ? 'Nenhum evento no período escolhido' : 'Nenhum evento nas categorias escolhidas'}
            descricao={
              filtrandoPeriodo
                ? 'Amplie o período, limpe as datas ou marque outra categoria para ver os eventos.'
                : 'Marque outra categoria acima para ver os eventos.'
            }
            compacto
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <ol className="divide-y divide-slate-100">
            {visiveis.map((item) => {
              const { Icone, marcador } = CATEGORIAS[item.categoria] ?? CATEGORIAS.Observação;
              // A vacinação é um dia de calendário: aparece sem hora e sem deslocamento de fuso.
              const vacinacao = item.categoria === 'Vacinação';

              return (
                <li key={item.chave}>
                  <button
                    type="button"
                    onClick={() => aoSelecionar(item)}
                    className="flex w-full items-start gap-3 px-4 py-3 text-left transition hover:bg-slate-50 sm:px-5"
                  >
                    {/* A data em que o registro foi feito vem primeiro, para achar o evento pelo dia. */}
                    <time dateTime={diaDoEvento(item)} className="w-24 shrink-0 pt-0.5 text-right tabular-nums">
                      <span className="block text-sm font-semibold text-slate-800">
                        {vacinacao ? formatarDia(item.data) : formatarData(item.data)}
                      </span>
                      {!vacinacao && <span className="block text-xs text-slate-500">{formatarHora(item.data)}</span>}
                    </time>

                    <span
                      className={`mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-white ${marcador}`}
                      aria-hidden="true"
                    >
                      <Icone size={14} />
                    </span>

                    <span className="min-w-0 flex-1">
                      <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                        <span className="font-semibold text-slate-900">{item.tipo}</span>
                        <EtiquetaCategoria categoria={item.categoria} />
                        {item.escalaDor != null && (
                          <Etiqueta className={estiloEscalaDor(item.escalaDor)}>Dor {item.escalaDor}/10</Etiqueta>
                        )}
                        {/* RN-004: a edição é sinalizada, preservando a rastreabilidade. */}
                        {item.editado && (
                          <Etiqueta className="bg-alerta-claro text-amber-800">
                            <Pencil size={11} />
                            Editado
                          </Etiqueta>
                        )}
                      </span>
                      <span className="mt-0.5 block truncate text-sm text-slate-700">{item.descricao}</span>
                      {item.autor && <span className="mt-0.5 block text-xs text-slate-500">{item.autor}</span>}
                    </span>

                    <ChevronRight size={18} className="mt-1 shrink-0 text-slate-300" aria-hidden="true" />
                  </button>
                </li>
              );
            })}
          </ol>
        </Card>
      )}
    </>
  );
}

interface RegistroProps {
  item: ItemLinhaTempo | null;
  exibeObservacoes: boolean;
  /** Botões de ação do registro (abrir na aba, corrigir, emitir receita...). */
  acoes?: ReactNode;
  aoFechar: () => void;
}

/** Registro completo de um evento da linha do tempo: todos os campos, mídias e anotações. */
export function ModalRegistroCompleto({ item, exibeObservacoes, acoes, aoFechar }: RegistroProps) {
  if (!item) return null;

  const campos = Object.entries(item.campos);

  return (
    <Modal
      aberto
      titulo={item.tipo}
      descricao={`${formatarDataDoEvento(item)}${item.autor ? ` · ${item.autor}` : ''}`}
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <div className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <EtiquetaCategoria categoria={item.categoria} />
          {item.escalaDor != null && (
            <Etiqueta className={estiloEscalaDor(item.escalaDor)}>Dor {item.escalaDor}/10</Etiqueta>
          )}
          {item.pesoKg != null && <Etiqueta className="bg-slate-100 text-slate-600">{formatarPeso(item.pesoKg)}</Etiqueta>}
          {item.editado && (
            <Etiqueta className="bg-alerta-claro text-amber-800">
              <Pencil size={11} />
              Editado — a versão anterior está no histórico
            </Etiqueta>
          )}
        </div>

        {campos.length > 0 ? (
          <dl className="grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-2">
            {campos.map(([rotulo, valor]) => (
              <div key={rotulo} className={valor.length > 80 ? 'sm:col-span-2' : undefined}>
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{rotulo}</dt>
                <dd className="mt-0.5 whitespace-pre-line text-sm text-slate-700">{valor}</dd>
              </div>
            ))}
          </dl>
        ) : (
          <div>
            <p className="text-sm font-medium text-slate-800">{item.descricao}</p>
            {item.detalhes && <p className="mt-1 whitespace-pre-line text-sm text-slate-600">{item.detalhes}</p>}
          </div>
        )}

        {/* HU-011, CA-4: fotos e vídeos da sessão. */}
        {item.midias.length > 0 && (
          <div>
            <p className="vc-rotulo">Fotografias e vídeos</p>
            <ul className="grid gap-2 sm:grid-cols-3">
              {item.midias.map((midia) => (
                <li key={midia.id} className="overflow-hidden rounded-xl border border-slate-200">
                  {midia.tipo === 'Video' ? (
                    <video src={urlDoArquivo(midia.urlArquivo)} controls className="h-28 w-full bg-slate-900 object-cover" />
                  ) : (
                    <a href={urlDoArquivo(midia.urlArquivo)} target="_blank" rel="noreferrer">
                      <img
                        src={urlDoArquivo(midia.urlArquivo)}
                        alt={midia.nomeArquivo}
                        className="h-28 w-full object-cover transition hover:opacity-90"
                        loading="lazy"
                      />
                    </a>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* RN-003: o bloco só existe quando o perfil autoriza; para o tutor a lista chega vazia da API. */}
        {exibeObservacoes && item.observacoesInternas.length > 0 && (
          <div className="rounded-xl border border-amber-200 bg-amber-50/70 p-4">
            <p className="mb-2 flex items-center gap-1.5 text-xs font-bold uppercase tracking-wide text-amber-800">
              <Lock size={12} />
              Observação interna — não visível ao tutor
            </p>

            {item.observacoesInternas.map((observacao) => (
              <div key={observacao.id} className="mt-2 first:mt-0">
                <p className="text-sm text-amber-900">{observacao.conteudo}</p>
                <p className="mt-0.5 text-xs text-amber-700">
                  {observacao.autor} · {formatarDataHora(observacao.dataRegistro)}
                </p>
              </div>
            ))}
          </div>
        )}

        {acoes && <div className="flex flex-wrap justify-end gap-2 border-t border-slate-200 pt-4">{acoes}</div>}
      </div>
    </Modal>
  );
}
