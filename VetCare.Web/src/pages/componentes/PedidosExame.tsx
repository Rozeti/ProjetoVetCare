import { useState, type FormEvent } from 'react';
import { FileText, FlaskConical, GitBranch, Loader2, Pencil, Printer, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Avaliacao, PedidoExame, Tratamento } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { formatarData, formatarDataHora, resumirTexto } from '../../utils/formato';
import { imprimirPedidoExame } from '../../utils/impressao';
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

/** Mesmos limites de CriarPedidoExameDTO e AtualizarPedidoExameDTO. */
const LIMITE_EXAMES = 2000;
const LIMITE_JUSTIFICATIVA = 2000;

/** Os exames do pedido, um por linha. */
function examesDo(pedido: PedidoExame): string[] {
  return pedido.exames
    .split(/\r?\n/)
    .map((exame) => exame.trim())
    .filter(Boolean);
}

interface ListaProps {
  pedidos: PedidoExame[];
  nomeClinica: string;
  /** Há avaliação ou tratamento ativo para originar um pedido. */
  temOrigens: boolean;
  /** Abre o formulário de pedido; ausente quando o prontuário é só consulta. */
  aoPedir?: () => void;
  /** Abre o pedido para edição; ausente para quem não pode alterá-lo. */
  aoEditar?: (pedido: PedidoExame) => void;
  aoAtualizar: (mensagem?: string) => void;
  /** Leva à avaliação ou ao tratamento de origem. */
  aoVerAvaliacao?: (avaliacaoId: string) => void;
  aoVerTratamento?: (tratamentoId: string) => void;
  /** Filtro de origem vindo de um atalho ("avaliacao:<id>" ou "tratamento:<id>"). */
  filtroInicial?: string;
  /** Pedido que chegou por um atalho: abre expandido. */
  emFoco?: string;
}

/**
 * Pedidos de exame do animal em linhas compactas — data, exames e a origem (a avaliação ou o
 * tratamento em que foram pedidos). Ao expandir, aparecem os exames, a justificativa, quem
 * assina e as ações. Quem assinou o pedido (e a administração) pode editá-lo ou excluí-lo; a
 * exclusão tira o pedido do prontuário e fica registrada na auditoria.
 */
export function PedidosExame({
  pedidos,
  nomeClinica,
  temOrigens,
  aoPedir,
  aoEditar,
  aoAtualizar,
  aoVerAvaliacao,
  aoVerTratamento,
  filtroInicial = '',
  emFoco,
}: ListaProps) {
  const { temPerfil, ehVeterinario, usuario } = useAuth();
  const { confirmarExclusao } = useConfirmacao();
  const { aberto, alternar } = useItensAbertos(emFoco);

  const [erro, setErro] = useState('');
  const [excluindo, setExcluindo] = useState('');
  const [filtro, setFiltro] = useState(filtroInicial);

  const visiveis = filtrarPorOrigem(pedidos, filtro);

  // O veterinário altera só os pedidos que ele assinou; a administração, qualquer um.
  const podeAlterar = (pedido: PedidoExame) =>
    temPerfil('Administrador', 'Veterinario') && (!ehVeterinario || pedido.veterinarioId === usuario?.veterinarioId);

  async function excluir(pedido: PedidoExame) {
    const confirmado = await confirmarExclusao(
      <>
        Excluir o pedido de exame emitido em <strong>{formatarData(pedido.dataEmissao)}</strong>? Ele sai do
        prontuário e não pode ser recuperado; a exclusão fica registrada na auditoria.
      </>,
      'Excluir pedido',
    );

    if (!confirmado) return;

    setErro('');
    setExcluindo(pedido.id);

    try {
      await api.delete(`/api/pedidos-exame/${pedido.id}`);
      aoAtualizar('Pedido de exame excluído.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível excluir o pedido de exame.'));
    } finally {
      setExcluindo('');
    }
  }

  return (
    <Card className="overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
        <div>
          <h2 className="font-semibold text-slate-900">Pedidos de exame</h2>
          <p className="text-xs text-slate-500">Exames pedidos nas avaliações e nos tratamentos, para consulta e impressão.</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <FiltroPorOrigem itens={pedidos} valor={filtro} aoMudar={setFiltro} />

          {aoPedir && temOrigens && (
            <button type="button" className="vc-botao-primario" onClick={aoPedir}>
              <FlaskConical size={16} />
              Pedir exames
            </button>
          )}
        </div>
      </div>

      {erro && (
        <Alerta flutuante tipo="erro" aoFechar={() => setErro('')}>
          {erro}
        </Alerta>
      )}

      {pedidos.length === 0 ? (
        <SemDados
          icone={<FlaskConical size={40} />}
          titulo="Nenhum pedido de exame"
          descricao={
            temOrigens
              ? 'Os pedidos feitos nas avaliações e nos tratamentos ficam aqui para consulta e impressão.'
              : 'Os pedidos de exame são feitos em uma avaliação ou em um tratamento. Registre a avaliação do animal primeiro.'
          }
        />
      ) : (
        <ul className="divide-y divide-slate-100">
          {visiveis.map((pedido) => {
            const dataDoPedido = formatarData(pedido.dataEmissao);
            const exames = examesDo(pedido);

            return (
              <ItemExpansivel
                key={pedido.id}
                ancora={`pedido-${pedido.id}`}
                aberto={aberto(pedido.id)}
                aoAlternar={() => alternar(pedido.id)}
                emFoco={pedido.id === emFoco}
                rotulo={`Pedido de exame de ${dataDoPedido}`}
                titulo={
                  <>
                    <span className="font-semibold text-slate-900">Pedido de {dataDoPedido}</span>
                    {/* Só os pedidos antigos podem estar cancelados: hoje o pedido é excluído. */}
                    {pedido.status === 'Cancelado' && <Etiqueta className="bg-slate-200 text-slate-600">Cancelado</Etiqueta>}
                  </>
                }
                linhas={[
                  <LinhaDaOrigem key="origem" item={pedido} />,
                  [exames.join(', '), pedido.nomeVeterinario].filter(Boolean).join(' · '),
                ]}
                atalhos={
                  <button
                    type="button"
                    onClick={() => imprimirPedidoExame(pedido, nomeClinica)}
                    className="rounded-lg p-2 text-slate-500 transition hover:bg-brand-100 hover:text-brand"
                    title="Imprimir pedido"
                    aria-label={`Imprimir o pedido de ${dataDoPedido}`}
                  >
                    <Printer size={16} />
                  </button>
                }
              >
                <Topico titulo="Origem" icone={<GitBranch size={14} />} semCaixa>
                  <OrigemDoDocumento item={pedido} aoVerAvaliacao={aoVerAvaliacao} aoVerTratamento={aoVerTratamento} />
                </Topico>

                <Topico
                  titulo={exames.length === 1 ? 'Exame solicitado' : `Exames solicitados (${exames.length})`}
                  icone={<FlaskConical size={14} />}
                  semCaixa
                >
                  <ul className="grid gap-2 sm:grid-cols-2">
                    {exames.map((exame, indice) => (
                      <li
                        key={`${exame}-${indice}`}
                        className="flex items-start gap-2 rounded-xl border border-slate-200/80 bg-slate-50 px-4 py-2.5 text-sm font-medium text-slate-800"
                      >
                        <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-brand" aria-hidden="true" />
                        {exame}
                      </li>
                    ))}
                  </ul>
                </Topico>

                {pedido.justificativa && (
                  <Topico titulo="Justificativa clínica" icone={<FileText size={14} />}>
                    {pedido.justificativa}
                  </Topico>
                )}

                <dl className="grid gap-3 sm:grid-cols-2">
                  <Dado rotulo="Emitido em">{formatarDataHora(pedido.dataEmissao)}</Dado>
                  <Dado rotulo="Veterinário">
                    {pedido.nomeVeterinario}
                    {pedido.crmv && <span className="block text-xs font-normal text-slate-500">{pedido.crmv}</span>}
                  </Dado>
                </dl>

                {pedido.status === 'Cancelado' && pedido.motivoCancelamento && (
                  <p className="text-sm text-slate-500">
                    <span className="font-semibold text-slate-700">Motivo do cancelamento:</span> {pedido.motivoCancelamento}
                  </p>
                )}

                <BarraDeAcoes>
                  <button type="button" className="vc-botao-secundario" onClick={() => imprimirPedidoExame(pedido, nomeClinica)}>
                    <Printer size={15} />
                    Imprimir pedido
                  </button>

                  {aoEditar && podeAlterar(pedido) && pedido.status === 'Ativo' && (
                    <button type="button" className="vc-botao-secundario" onClick={() => aoEditar(pedido)}>
                      <Pencil size={15} />
                      Editar pedido
                    </button>
                  )}

                  {podeAlterar(pedido) && (
                    <button
                      type="button"
                      onClick={() => excluir(pedido)}
                      disabled={excluindo === pedido.id}
                      className="vc-botao-secundario text-perigo hover:bg-perigo-claro"
                    >
                      {excluindo === pedido.id ? <Loader2 className="animate-spin" size={15} /> : <Trash2 size={15} />}
                      Excluir pedido
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

export interface PropsDoFormularioDePedido {
  avaliacoes: Avaliacao[];
  /** Sessões registradas do animal: descrevem a origem do pedido feito no registro de uma sessão. */
  sessoes: SessaoRegistrada[];
  tratamentos: Tratamento[];
  /** Origem já definida pelo botão que abriu o formulário (avaliação, tratamento ou registro da sessão). */
  origem?: OrigemClinica | null;
  /** Pedido em edição; ausente para um pedido novo. */
  pedido?: PedidoExame | null;
  aoCancelar: () => void;
  aoSalvar: (mensagem: string) => void;
}

interface ModalProps extends Omit<PropsDoFormularioDePedido, 'aoCancelar'> {
  aberto: boolean;
  aoFechar: () => void;
}

/** Pedido de exames feito em uma avaliação ou em um tratamento: emissão ou edição. */
export function ModalPedidoExame({ aberto, aoFechar, ...props }: ModalProps) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos novos.
  if (!aberto) return null;

  const { pedido } = props;

  return (
    <Modal
      aberto
      titulo={pedido ? 'Editar pedido de exame' : 'Pedir exames'}
      descricao={
        pedido
          ? `Pedido emitido em ${formatarData(pedido.dataEmissao)}. A versão anterior fica guardada no histórico.`
          : 'O pedido fica no prontuário, ligado à avaliação ou ao tratamento de origem, e pode ser impresso para o tutor.'
      }
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <FormularioDePedido {...props} aoCancelar={aoFechar} />
    </Modal>
  );
}

function origemDoPedido(pedido: PedidoExame): string {
  if (pedido.tipoOrigem === 'Avaliação' && pedido.avaliacaoId) return `avaliacao:${pedido.avaliacaoId}`;
  if (pedido.atendimentoId) return `sessao:${pedido.atendimentoId}`;
  return pedido.tratamentoId ? `tratamento:${pedido.tratamentoId}` : '';
}

/** Campos do pedido, usados no modal de pedido e no de receita ou pedido de exame da sessão. */
export function FormularioDePedido({
  avaliacoes,
  sessoes,
  tratamentos,
  origem: origemInicial,
  pedido,
  aoCancelar,
  aoSalvar,
}: PropsDoFormularioDePedido) {
  const { ehVeterinario } = useAuth();
  const editando = !!pedido;

  const ativos = tratamentosAtivos(tratamentos);
  const [origem, setOrigem] = useState(() => {
    if (pedido) return origemDoPedido(pedido);
    if (origemInicial) return valorDaOrigem(origemInicial);
    return avaliacoes.length === 1 && ativos.length === 0 ? `avaliacao:${avaliacoes[0].id}` : '';
  });
  const [exames, setExames] = useState(pedido?.exames ?? '');
  const [justificativa, setJustificativa] = useState(pedido?.justificativa ?? '');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const { avaliacaoId, tratamentoId, atendimentoId } = lerOrigem(origem);
  const avaliacaoEscolhida = avaliacoes.find((a) => a.id === avaliacaoId);
  const sessaoDaOrigem = atendimentoId ? sessoes.find((s) => s.id === atendimentoId) : undefined;
  const tratamentoDaOrigem = tratamentoId ? tratamentos.find((t) => t.id === tratamentoId) : undefined;

  // O pedido feito no tratamento não muda de origem; o de uma avaliação pode passar a outra avaliação.
  const origemTravada = !!origemInicial || (!!pedido && pedido.tipoOrigem !== 'Avaliação');

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!origem) {
      setErro('Selecione a origem do pedido: a avaliação ou o tratamento em que os exames foram pedidos.');
      return;
    }

    if (exames.trim().length < 2) {
      setErro('Informe os exames solicitados.');
      return;
    }

    if (exames.length > LIMITE_EXAMES || justificativa.length > LIMITE_JUSTIFICATIVA) {
      setErro(`Os exames e a justificativa devem ter até ${LIMITE_EXAMES} caracteres cada.`);
      return;
    }

    setSalvando(true);

    try {
      if (pedido) {
        await api.put(`/api/pedidos-exame/${pedido.id}`, {
          avaliacaoId: pedido.tipoOrigem === 'Avaliação' ? avaliacaoId : null,
          // O vínculo antigo de um pedido da avaliação com um tratamento é mantido.
          tratamentoId: pedido.tratamentoId ?? null,
          exames: exames.trim(),
          justificativa: justificativa.trim(),
        });
        aoSalvar('Pedido de exame atualizado.');
      } else {
        await api.post('/api/pedidos-exame', {
          // A avaliação, a sessão (a API liga o tratamento dela) ou o próprio tratamento.
          ...lerOrigem(origem),
          exames: exames.trim(),
          justificativa: justificativa.trim(),
        });
        aoSalvar('Pedido de exame emitido com sucesso.');
      }
    } catch (falha) {
      setErro(
        mensagemDeErro(falha, editando ? 'Não foi possível salvar o pedido de exame.' : 'Não foi possível emitir o pedido de exame.'),
      );
    } finally {
      setSalvando(false);
    }
  }

  if (!origemTravada && !editando && avaliacoes.length === 0 && ativos.length === 0) {
    return (
      <Alerta tipo="aviso">
        O pedido de exame é feito em uma avaliação ou em um tratamento. Registre a avaliação do animal antes de
        pedir exames.
      </Alerta>
    );
  }

  const assinatura = pedido
    ? `Assinado por ${pedido.nomeVeterinario}.`
    : ehVeterinario
      ? ''
      : sessaoDaOrigem?.nomeVeterinario
        ? `Assinado por ${sessaoDaOrigem.nomeVeterinario}, que atendeu a sessão.`
        : avaliacaoEscolhida
          ? `Assinado por ${avaliacaoEscolhida.nomeVeterinario}, responsável pela avaliação.`
          : tratamentoDaOrigem
            ? `Assinado por ${tratamentoDaOrigem.nomeVeterinario}, responsável pelo tratamento.`
            : '';

  return (
    <form onSubmit={aoEnviar} className="space-y-4" noValidate>
      {origemTravada ? (
        <ResumoDaOrigem origem={descreverOrigem(origem, avaliacoes, sessoes, tratamentos) || pedido?.origem || ''} />
      ) : (
        <CampoOrigem
          valor={origem}
          aoMudar={setOrigem}
          avaliacoes={avaliacoes}
          // Um pedido de avaliação já emitido só troca por outra avaliação.
          tratamentos={editando ? [] : ativos}
          dica={
            editando
              ? 'A avaliação em que os exames foram pedidos.'
              : 'A avaliação ou o tratamento em que os exames foram pedidos.'
          }
        />
      )}

      <Campo rotulo="Exames solicitados" obrigatorio dica="Um exame por linha.">
        <textarea
          className="vc-campo"
          rows={4}
          value={exames}
          onChange={(e) => setExames(e.target.value)}
          placeholder={'Hemograma completo\nRaio-X da articulação coxofemoral'}
          maxLength={LIMITE_EXAMES}
        />
      </Campo>

      <Campo rotulo="Justificativa clínica" dica={`${justificativa.length}/${LIMITE_JUSTIFICATIVA} caracteres`}>
        <textarea
          className="vc-campo"
          rows={2}
          value={justificativa}
          onChange={(e) => setJustificativa(e.target.value)}
          placeholder={avaliacaoEscolhida ? `Ex.: investigar ${resumirTexto(avaliacaoEscolhida.diagnosticoPossivel, 60)}` : ''}
          maxLength={LIMITE_JUSTIFICATIVA}
        />
      </Campo>

      {assinatura && <p className="text-sm text-slate-600">{assinatura}</p>}

      {erro && <Alerta tipo="erro">{erro}</Alerta>}

      <div className="flex justify-end gap-2 pt-1">
        <button type="button" className="vc-botao-secundario" onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </button>
        <button type="submit" className="vc-botao-primario" disabled={salvando}>
          {salvando && <Loader2 className="animate-spin" size={16} />}
          {editando ? 'Salvar alterações' : 'Emitir pedido'}
        </button>
      </div>
    </form>
  );
}
