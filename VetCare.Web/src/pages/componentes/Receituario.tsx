import { useState, type FormEvent } from 'react';
import { Ban, CalendarClock, GitBranch, Loader2, MessageSquareText, Pill, Plus, Printer, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Avaliacao, Prescricao, Tratamento } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { SeletorVeterinario } from '../../components/SeletorVeterinario';
import { formatarData, formatarDataHora, formatarDia, paraValorInputData, paraValorInputDia } from '../../utils/formato';
import { imprimirReceita } from '../../utils/impressao';
import { CampoOrigem, FiltroPorOrigem, LinhaDaOrigem, OrigemDoDocumento, ResumoDaOrigem } from './VinculoClinico';
import { BarraDeAcoes, Dado, ItemExpansivel, Topico } from './RegistroClinico';
import { useItensAbertos } from '../../hooks/useItensAbertos';
import {
  descreverOrigem,
  filtrarPorOrigem,
  lerOrigem,
  tratamentosAtivos,
  valorDaOrigem,
  type OrigemClinica,
  type SessaoRegistrada,
} from '../../utils/vinculoClinico';

const VIAS = ['Oral', 'Tópica', 'Intramuscular', 'Subcutânea', 'Intravenosa', 'Oftálmica', 'Otológica'];

/** Mesmos limites de CriarPrescricaoDTO e da validade aceita pela API. */
const LIMITES = {
  medicamento: 200,
  dosagem: 120,
  frequencia: 120,
  duracao: 120,
  observacao: 500,
  orientacoes: 2000,
};

const VALIDADE_MAXIMA_EM_DIAS = 365;

function validadeMaxima(): Date {
  const data = new Date();
  data.setDate(data.getDate() + VALIDADE_MAXIMA_EM_DIAS);
  return data;
}

function proximosTrintaDias(): Date {
  const data = new Date();
  data.setDate(data.getDate() + 30);
  return data;
}

interface ItemFormulario {
  medicamento: string;
  dosagem: string;
  frequencia: string;
  duracao: string;
  via: string;
  observacao: string;
}

const ITEM_VAZIO: ItemFormulario = {
  medicamento: '',
  dosagem: '',
  frequencia: '',
  duracao: '',
  via: 'Oral',
  observacao: '',
};

interface Props {
  prescricoes: Prescricao[];
  nomeClinica: string;
  /** Abre o formulário de receita; ausente quando o prontuário é só consulta. */
  aoEmitir?: () => void;
  /** Recarrega o prontuário; a mensagem, quando vem, é exibida pela tela que o contém. */
  aoAtualizar: (mensagem?: string) => void;
  /** Leva à avaliação ou ao tratamento de origem. */
  aoVerAvaliacao?: (avaliacaoId: string) => void;
  aoVerTratamento?: (tratamentoId: string) => void;
  /** Filtro de origem vindo de um atalho ("avaliacao:<id>" ou "tratamento:<id>"). */
  filtroInicial?: string;
  /** Receita que chegou por um atalho: abre expandida. */
  emFoco?: string;
}

/** Ativa, com a validade já passada: a receita ainda existe, mas não serve mais. */
function vencida(prescricao: Prescricao): boolean {
  return (
    prescricao.status === 'Ativa' &&
    !!prescricao.validaAte &&
    paraValorInputDia(prescricao.validaAte) < paraValorInputData(new Date())
  );
}

/**
 * Medicação do paciente: as receitas em linhas compactas — data, situação, medicamentos e a
 * origem (a avaliação ou o tratamento em que foram prescritas). Ao expandir, aparecem os
 * medicamentos com dosagem, frequência, duração e via, as orientações, a validade, quem assina
 * e as ações de impressão e cancelamento. A lista pode ser filtrada pela origem.
 */
export function Receituario({
  prescricoes,
  nomeClinica,
  aoEmitir,
  aoAtualizar,
  aoVerAvaliacao,
  aoVerTratamento,
  filtroInicial = '',
  emFoco,
}: Props) {
  const { temPerfil } = useAuth();
  const podeCancelar = temPerfil('Administrador', 'Veterinario');
  const { perguntar } = useConfirmacao();
  const { aberto, alternar } = useItensAbertos(emFoco);

  const [erro, setErro] = useState('');
  const [cancelando, setCancelando] = useState('');
  const [filtro, setFiltro] = useState(filtroInicial);

  const visiveis = filtrarPorOrigem(prescricoes, filtro);

  async function cancelar(prescricao: Prescricao) {
    const { confirmado, texto: motivo } = await perguntar({
      titulo: 'Cancelar receita',
      mensagem: (
        <>
          Cancelar a receita emitida em <strong>{formatarData(prescricao.dataEmissao)}</strong>? Ela continua no
          prontuário como cancelada e, se for impressa, sai marcada como sem validade.
        </>
      ),
      rotuloConfirmar: 'Cancelar receita',
      rotuloCancelar: 'Voltar',
      perigo: true,
      campoTexto: { rotulo: 'Motivo do cancelamento', placeholder: 'Ex.: dosagem revista', obrigatorio: true },
    });

    if (!confirmado) {
      return;
    }

    if (motivo.length > 300) {
      setErro('O motivo do cancelamento deve ter até 300 caracteres.');
      return;
    }

    setErro('');
    setCancelando(prescricao.id);

    try {
      await api.patch(`/api/prescricoes/${prescricao.id}/cancelar`, { motivo });
      aoAtualizar('Receita cancelada. O registro permanece no histórico do paciente.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível cancelar a receita.'));
    } finally {
      setCancelando('');
    }
  }

  return (
    <Card className="overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
        <div>
          <h2 className="font-semibold text-slate-900">Medicação</h2>
          <p className="text-xs text-slate-500">
            Receitas prescritas nas avaliações e nos tratamentos, para consulta e impressão.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <FiltroPorOrigem itens={prescricoes} valor={filtro} aoMudar={setFiltro} />

          {aoEmitir && (
            <button type="button" className="vc-botao-primario" onClick={aoEmitir}>
              <Plus size={16} />
              Nova receita
            </button>
          )}
        </div>
      </div>

      {erro && (
        <Alerta flutuante tipo="erro" aoFechar={() => setErro('')}>
          {erro}
        </Alerta>
      )}

      {prescricoes.length === 0 ? (
        <SemDados
          icone={<Pill size={40} />}
          titulo="Nenhuma receita emitida"
          descricao="As receitas são emitidas em uma avaliação ou em um tratamento e ficam aqui para consulta e impressão."
        />
      ) : (
        <ul className="divide-y divide-slate-100">
          {visiveis.map((prescricao) => {
            const dataDaReceita = formatarData(prescricao.dataEmissao);
            const estaVencida = vencida(prescricao);

            return (
              <ItemExpansivel
                key={prescricao.id}
                ancora={`receita-${prescricao.id}`}
                aberto={aberto(prescricao.id)}
                aoAlternar={() => alternar(prescricao.id)}
                emFoco={prescricao.id === emFoco}
                rotulo={`Receita de ${dataDaReceita}`}
                titulo={
                  <>
                    <span className="font-semibold text-slate-900">Receita de {dataDaReceita}</span>
                    <Etiqueta
                      className={
                        prescricao.status !== 'Ativa'
                          ? 'bg-slate-200 text-slate-600'
                          : estaVencida
                            ? 'bg-alerta-claro text-amber-800'
                            : 'bg-sucesso-claro text-emerald-800'
                      }
                    >
                      {estaVencida ? 'Vencida' : prescricao.status}
                    </Etiqueta>
                  </>
                }
                linhas={[
                  <LinhaDaOrigem key="origem" item={prescricao} />,
                  [prescricao.itens.map((item) => item.medicamento).join(', '), prescricao.nomeVeterinario]
                    .filter(Boolean)
                    .join(' · '),
                ]}
                atalhos={
                  <button
                    type="button"
                    onClick={() => imprimirReceita(prescricao, nomeClinica)}
                    className="rounded-lg p-2 text-slate-500 transition hover:bg-brand-100 hover:text-brand"
                    title="Imprimir receita"
                    aria-label={`Imprimir a receita de ${dataDaReceita}`}
                  >
                    <Printer size={16} />
                  </button>
                }
              >
                <Topico titulo="Origem" icone={<GitBranch size={14} />} semCaixa>
                  <OrigemDoDocumento item={prescricao} aoVerAvaliacao={aoVerAvaliacao} aoVerTratamento={aoVerTratamento} />
                </Topico>

                <Topico
                  titulo={prescricao.itens.length === 1 ? 'Medicamento' : `Medicamentos (${prescricao.itens.length})`}
                  icone={<Pill size={14} />}
                  semCaixa
                >
                  <ul className="space-y-2">
                    {prescricao.itens.map((item) => (
                      <li key={item.id} className="rounded-xl border border-slate-200/80 bg-slate-50 px-4 py-3">
                        <p className="font-semibold text-slate-900">{item.medicamento}</p>
                        <dl className="mt-2 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-4">
                          {(
                            [
                              ['Dosagem', item.dosagem],
                              ['Frequência', item.frequencia],
                              ['Duração', item.duracao],
                              ['Via', item.via],
                            ] as const
                          ).map(([rotulo, valor]) => (
                            <div key={rotulo}>
                              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{rotulo}</dt>
                              <dd className="text-slate-800">{valor || '—'}</dd>
                            </div>
                          ))}
                        </dl>
                        {item.observacao && (
                          <p className="mt-2 border-t border-slate-200/80 pt-2 text-sm text-slate-700">
                            <span className="font-semibold text-slate-900">Observação: </span>
                            {item.observacao}
                          </p>
                        )}
                      </li>
                    ))}
                  </ul>
                </Topico>

                {prescricao.orientacoes && (
                  <Topico titulo="Orientações" icone={<MessageSquareText size={14} />}>
                    {prescricao.orientacoes}
                  </Topico>
                )}

                <dl className="grid gap-3 sm:grid-cols-3">
                  <Dado rotulo="Emitida em">{formatarDataHora(prescricao.dataEmissao)}</Dado>
                  <Dado rotulo="Válida até">
                    <span className="inline-flex items-center gap-1.5">
                      <CalendarClock size={14} className="text-slate-400" aria-hidden="true" />
                      {prescricao.validaAte ? formatarDia(prescricao.validaAte) : 'Sem validade informada'}
                      {estaVencida && <span className="text-xs font-semibold text-amber-700">(vencida)</span>}
                    </span>
                  </Dado>
                  <Dado rotulo="Veterinário">
                    {prescricao.nomeVeterinario}
                    {prescricao.crmv && <span className="block text-xs font-normal text-slate-500">{prescricao.crmv}</span>}
                  </Dado>
                </dl>

                <BarraDeAcoes>
                  <button type="button" className="vc-botao-secundario" onClick={() => imprimirReceita(prescricao, nomeClinica)}>
                    <Printer size={15} />
                    Imprimir receita
                  </button>

                  {podeCancelar && prescricao.status === 'Ativa' && (
                    <button
                      type="button"
                      onClick={() => cancelar(prescricao)}
                      disabled={cancelando === prescricao.id}
                      className="vc-botao-secundario text-perigo hover:bg-perigo-claro"
                    >
                      {cancelando === prescricao.id ? <Loader2 className="animate-spin" size={15} /> : <Ban size={15} />}
                      Cancelar receita
                    </button>
                  )}
                </BarraDeAcoes>
              </ItemExpansivel>
            );
          })}
        </ul>
      )}
    </Card>
  );
}

export interface PropsDoFormularioDeReceita {
  pacienteId: string;
  avaliacoes: Avaliacao[];
  /** Sessões registradas do animal: descrevem a origem da receita feita no registro de uma sessão. */
  sessoes: SessaoRegistrada[];
  tratamentos: Tratamento[];
  /** Origem já definida pelo botão que abriu o formulário; sem ela, o usuário escolhe. */
  origem?: OrigemClinica | null;
  aoCancelar: () => void;
  aoSalvar: (mensagem: string) => void;
}

interface ModalProps extends Omit<PropsDoFormularioDeReceita, 'aoCancelar'> {
  aberto: boolean;
  aoFechar: () => void;
}

/**
 * Emissão de receita. Toda receita vem de uma avaliação ou de um tratamento (direto nele ou no
 * registro de uma sessão dele): a origem é escolhida no formulário, ou já vem marcada quando a
 * emissão parte do próprio registro.
 */
export function ModalReceita({ aberto, aoFechar, ...props }: ModalProps) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos novos.
  if (!aberto) return null;

  return (
    <Modal
      aberto
      titulo="Nova receita"
      descricao="A receita fica no prontuário, ligada à avaliação ou ao tratamento de origem, e pode ser impressa para o tutor."
      aoFechar={aoFechar}
      largura="max-w-3xl"
    >
      <FormularioDeReceita {...props} aoCancelar={aoFechar} />
    </Modal>
  );
}

/** Campos da receita, usados no modal de receita e no de receita ou pedido de exame da sessão. */
export function FormularioDeReceita({
  pacienteId,
  avaliacoes,
  sessoes,
  tratamentos,
  origem: origemInicial,
  aoCancelar,
  aoSalvar,
}: PropsDoFormularioDeReceita) {
  const { ehVeterinario } = useAuth();

  const [origem, setOrigem] = useState(valorDaOrigem(origemInicial));
  const [itens, setItens] = useState<ItemFormulario[]>([{ ...ITEM_VAZIO }]);
  // O veterinário logado assina a própria receita; o administrador precisa escolher quem assina.
  const [veterinarioId, setVeterinarioId] = useState('');
  const [orientacoes, setOrientacoes] = useState('');
  const [validaAte, setValidaAte] = useState(paraValorInputData(proximosTrintaDias()));
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  // Limite inferior do campo de validade, calculado uma vez: o lint pede funções puras na renderização.
  const [amanha] = useState(() => paraValorInputData(new Date(Date.now() + 86_400_000)));

  const ativos = tratamentosAtivos(tratamentos);

  function atualizarItem(indice: number, campo: keyof ItemFormulario, valor: string) {
    setItens((atual) => atual.map((item, i) => (i === indice ? { ...item, [campo]: valor } : item)));
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!origem) {
      setErro('Informe a origem da receita: a avaliação ou o tratamento em que ela foi prescrita.');
      return;
    }

    const preenchidos = itens.filter((i) => i.medicamento.trim());

    if (preenchidos.length === 0) {
      setErro('Informe ao menos um medicamento.');
      return;
    }

    const semDosagem = preenchidos.find((i) => !i.dosagem.trim() || !i.frequencia.trim());

    if (semDosagem) {
      setErro(`Informe a dosagem e a frequência de ${semDosagem.medicamento}.`);
      return;
    }

    // Mesmos limites de CriarPrescricaoDTO: o erro aparece antes de a receita ir para a API.
    const itemInvalido = preenchidos.find(
      (i) =>
        i.medicamento.trim().length < 2 ||
        i.medicamento.trim().length > LIMITES.medicamento ||
        i.dosagem.trim().length > LIMITES.dosagem ||
        i.frequencia.trim().length > LIMITES.frequencia ||
        i.duracao.trim().length > LIMITES.duracao ||
        i.observacao.trim().length > LIMITES.observacao,
    );

    if (itemInvalido) {
      setErro(
        `Confira o item ${itemInvalido.medicamento || 'sem nome'}: medicamento com 2 a ${LIMITES.medicamento} caracteres, ` +
          `dosagem, frequência e duração com até ${LIMITES.dosagem}, observação com até ${LIMITES.observacao}.`,
      );
      return;
    }

    if (orientacoes.length > LIMITES.orientacoes) {
      setErro(`As orientações devem ter até ${LIMITES.orientacoes} caracteres.`);
      return;
    }

    const hoje = paraValorInputData(new Date());

    if (validaAte && validaAte <= hoje) {
      setErro('A validade da receita deve ser uma data futura.');
      return;
    }

    if (validaAte && validaAte > paraValorInputData(validadeMaxima())) {
      setErro(`A validade da receita não pode passar de ${VALIDADE_MAXIMA_EM_DIAS} dias.`);
      return;
    }

    if (!ehVeterinario && !veterinarioId) {
      setErro('Selecione o veterinário responsável pela receita.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/prescricoes', {
        pacienteId,
        veterinarioId: veterinarioId || null,
        // A avaliação, a sessão (a API liga o tratamento dela) ou o próprio tratamento.
        ...lerOrigem(origem),
        validaAte: validaAte || null,
        orientacoes: orientacoes.trim(),
        itens: preenchidos.map((i) => ({
          medicamento: i.medicamento.trim(),
          dosagem: i.dosagem.trim(),
          frequencia: i.frequencia.trim(),
          duracao: i.duracao.trim(),
          via: i.via,
          observacao: i.observacao.trim(),
        })),
      });

      aoSalvar('Receita emitida com sucesso.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível emitir a receita.'));
    } finally {
      setSalvando(false);
    }
  }

  if (!origemInicial && avaliacoes.length === 0 && ativos.length === 0) {
    return (
      <Alerta tipo="aviso">
        A receita é emitida em uma avaliação ou em um tratamento. Registre a avaliação do animal antes de
        prescrever.
      </Alerta>
    );
  }

  return (
    <form onSubmit={aoEnviar} className="space-y-4" noValidate>
      {origemInicial ? (
        <ResumoDaOrigem origem={descreverOrigem(origem, avaliacoes, sessoes, tratamentos)} />
      ) : (
        <CampoOrigem
          valor={origem}
          aoMudar={setOrigem}
          avaliacoes={avaliacoes}
          tratamentos={ativos}
          dica="A avaliação ou o tratamento em que o medicamento foi prescrito."
        />
      )}

      {itens.map((item, indice) => (
        <fieldset key={indice} className="rounded-xl border border-slate-200 p-4">
          <legend className="flex items-center gap-2 px-2 text-sm font-semibold text-slate-700">
            Medicamento {indice + 1}
            {itens.length > 1 && (
              <button
                type="button"
                onClick={() => setItens((atual) => atual.filter((_, i) => i !== indice))}
                className="text-slate-400 transition hover:text-perigo"
                aria-label={`Remover medicamento ${indice + 1}`}
              >
                <Trash2 size={14} />
              </button>
            )}
          </legend>

          <div className="space-y-3">
            <Campo rotulo="Medicamento" obrigatorio>
              <input
                className="vc-campo"
                value={item.medicamento}
                onChange={(e) => atualizarItem(indice, 'medicamento', e.target.value)}
                placeholder="Nome e concentração"
                maxLength={LIMITES.medicamento}
              />
            </Campo>

            <div className="grid gap-3 sm:grid-cols-2">
              <Campo rotulo="Dosagem" obrigatorio>
                <input
                  className="vc-campo"
                  value={item.dosagem}
                  onChange={(e) => atualizarItem(indice, 'dosagem', e.target.value)}
                  placeholder="Ex.: 1 comprimido"
                  maxLength={LIMITES.dosagem}
                />
              </Campo>

              <Campo rotulo="Frequência" obrigatorio>
                <input
                  className="vc-campo"
                  value={item.frequencia}
                  onChange={(e) => atualizarItem(indice, 'frequencia', e.target.value)}
                  placeholder="Ex.: a cada 12 horas"
                  maxLength={LIMITES.frequencia}
                />
              </Campo>

              <Campo rotulo="Duração">
                <input
                  className="vc-campo"
                  value={item.duracao}
                  onChange={(e) => atualizarItem(indice, 'duracao', e.target.value)}
                  placeholder="Ex.: por 7 dias"
                  maxLength={LIMITES.duracao}
                />
              </Campo>

              <Campo rotulo="Via">
                <select className="vc-campo" value={item.via} onChange={(e) => atualizarItem(indice, 'via', e.target.value)}>
                  {VIAS.map((via) => (
                    <option key={via}>{via}</option>
                  ))}
                </select>
              </Campo>
            </div>

            <Campo rotulo="Observação">
              <input
                className="vc-campo"
                value={item.observacao}
                onChange={(e) => atualizarItem(indice, 'observacao', e.target.value)}
                placeholder="Ex.: administrar junto com alimento"
                maxLength={LIMITES.observacao}
              />
            </Campo>
          </div>
        </fieldset>
      ))}

      <button
        type="button"
        className="vc-botao-secundario w-full"
        onClick={() => setItens((atual) => [...atual, { ...ITEM_VAZIO }])}
      >
        <Plus size={16} />
        Adicionar medicamento
      </button>

      <div className="grid gap-4 sm:grid-cols-2">
        <SeletorVeterinario valor={veterinarioId} aoMudar={setVeterinarioId} obrigatorio />

        <Campo rotulo="Válida até" dica={`Data futura, até ${VALIDADE_MAXIMA_EM_DIAS} dias. Em branco, 30 dias.`}>
          <input
            type="date"
            className="vc-campo"
            min={amanha}
            max={paraValorInputData(validadeMaxima())}
            value={validaAte}
            onChange={(e) => setValidaAte(e.target.value)}
          />
        </Campo>
      </div>

      <Campo rotulo="Orientações gerais" dica={`${orientacoes.length}/${LIMITES.orientacoes} caracteres`}>
        <textarea
          className="vc-campo"
          rows={3}
          value={orientacoes}
          onChange={(e) => setOrientacoes(e.target.value)}
          placeholder="Cuidados, sinais de alerta e quando retornar à clínica."
          maxLength={LIMITES.orientacoes}
        />
      </Campo>

      {erro && <Alerta tipo="erro">{erro}</Alerta>}

      <div className="flex justify-end gap-2 pt-1">
        <button type="button" className="vc-botao-secundario" onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </button>
        <button type="submit" className="vc-botao-primario" disabled={salvando}>
          {salvando && <Loader2 className="animate-spin" size={16} />}
          Emitir receita
        </button>
      </div>
    </form>
  );
}
