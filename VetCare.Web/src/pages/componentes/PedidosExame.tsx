import { useState, type FormEvent } from 'react';
import { FlaskConical, Loader2, Pencil, Printer, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Avaliacao, PedidoExame, Tratamento } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { formatarData, resumirTexto } from '../../utils/formato';
import { imprimirPedidoExame } from '../../utils/impressao';
import { CampoOrigem, CampoTratamento, FiltroPorTratamento, ReferenciaAoTratamento, ResumoDaOrigem } from './VinculoClinico';
import {
  descreverOrigem,
  filtrarPorTratamento,
  lerOrigem,
  tratamentoSugerido,
  tratamentosParaVinculo,
  valorDaOrigem,
  type OrigemClinica,
  type SessaoRegistrada,
} from '../../utils/vinculoClinico';

/** Mesmos limites de CriarPedidoExameDTO e AtualizarPedidoExameDTO. */
const LIMITE_EXAMES = 2000;
const LIMITE_JUSTIFICATIVA = 2000;

interface ListaProps {
  pedidos: PedidoExame[];
  nomeClinica: string;
  /** Há avaliação ou sessão registrada para originar um pedido. */
  temOrigens: boolean;
  /** Abre o formulário de pedido; ausente quando o prontuário é só consulta. */
  aoPedir?: () => void;
  /** Abre o pedido para edição; ausente para quem não pode alterá-lo. */
  aoEditar?: (pedido: PedidoExame) => void;
  aoAtualizar: (mensagem?: string) => void;
  /** Leva ao tratamento do pedido, na aba Tratamentos. */
  aoVerTratamento?: (tratamentoId: string) => void;
}

/**
 * Pedidos de exame do animal. Cada um mostra o tratamento para o qual foi feito e de onde veio
 * (avaliação ou sessão); a lista pode ser filtrada por tratamento. Quem assinou o pedido (e a
 * administração) pode editá-lo ou excluí-lo; a exclusão tira o pedido do prontuário e fica
 * registrada na auditoria.
 */
export function PedidosExame({
  pedidos,
  nomeClinica,
  temOrigens,
  aoPedir,
  aoEditar,
  aoAtualizar,
  aoVerTratamento,
}: ListaProps) {
  const { temPerfil, ehVeterinario, usuario } = useAuth();
  const { confirmarExclusao } = useConfirmacao();

  const [erro, setErro] = useState('');
  const [excluindo, setExcluindo] = useState('');
  const [filtro, setFiltro] = useState('');

  const visiveis = filtrarPorTratamento(pedidos, filtro);

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
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
        <h2 className="font-semibold text-slate-900">Pedidos de exame</h2>

        <div className="flex flex-wrap items-center gap-2">
          <FiltroPorTratamento itens={pedidos} valor={filtro} aoMudar={setFiltro} />

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
              ? 'Os pedidos feitos nas avaliações e nas sessões ficam aqui para consulta e impressão.'
              : 'Os pedidos de exame são feitos a partir de uma avaliação ou de uma sessão. Registre a avaliação do animal primeiro.'
          }
        />
      ) : (
        <ul className="divide-y divide-slate-100">
          {visiveis.map((pedido) => (
            <li key={pedido.id} id={`pedido-${pedido.id}`} className="scroll-mt-4 px-5 py-4">
              <div className="mb-2 flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-slate-900">Pedido de {formatarData(pedido.dataEmissao)}</span>
                    {/* Só os pedidos antigos podem estar cancelados: hoje o pedido é excluído. */}
                    {pedido.status === 'Cancelado' && (
                      <Etiqueta className="bg-slate-200 text-slate-600">Cancelado</Etiqueta>
                    )}
                  </div>
                  <p className="mt-0.5 text-xs text-slate-500">
                    {pedido.nomeVeterinario}
                    {pedido.crmv && ` · ${pedido.crmv}`}
                  </p>
                  <p className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-slate-500">
                    <ReferenciaAoTratamento item={pedido} aoVerTratamento={aoVerTratamento} />
                    {pedido.origem && <span>Origem: {pedido.origem}</span>}
                  </p>
                </div>

                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    onClick={() => imprimirPedidoExame(pedido, nomeClinica)}
                    className="rounded-lg p-2 text-slate-500 transition hover:bg-brand-100 hover:text-brand"
                    title="Imprimir pedido"
                    aria-label={`Imprimir o pedido de ${formatarData(pedido.dataEmissao)}`}
                  >
                    <Printer size={16} />
                  </button>

                  {aoEditar && podeAlterar(pedido) && pedido.status === 'Ativo' && (
                    <button
                      type="button"
                      onClick={() => aoEditar(pedido)}
                      className="rounded-lg p-2 text-slate-500 transition hover:bg-brand-100 hover:text-brand"
                      title="Editar pedido"
                      aria-label={`Editar o pedido de ${formatarData(pedido.dataEmissao)}`}
                    >
                      <Pencil size={16} />
                    </button>
                  )}

                  {podeAlterar(pedido) && (
                    <button
                      type="button"
                      onClick={() => excluir(pedido)}
                      disabled={excluindo === pedido.id}
                      className="rounded-lg p-2 text-slate-500 transition hover:bg-perigo-claro hover:text-perigo disabled:opacity-50"
                      title="Excluir pedido"
                      aria-label={`Excluir o pedido de ${formatarData(pedido.dataEmissao)}`}
                    >
                      {excluindo === pedido.id ? <Loader2 className="animate-spin" size={16} /> : <Trash2 size={16} />}
                    </button>
                  )}
                </div>
              </div>

              <ul className="flex flex-wrap gap-2">
                {pedido.exames
                  .split(/\r?\n/)
                  .map((exame) => exame.trim())
                  .filter(Boolean)
                  .map((exame, indice) => (
                    <li key={`${exame}-${indice}`} className="rounded-lg bg-slate-50 px-3 py-1.5 text-sm text-slate-700">
                      {exame}
                    </li>
                  ))}
              </ul>

              {pedido.justificativa && (
                <p className="mt-2 text-sm text-slate-600">
                  <strong className="text-slate-700">Justificativa:</strong> {pedido.justificativa}
                </p>
              )}

              {pedido.status === 'Cancelado' && pedido.motivoCancelamento && (
                <p className="mt-1 text-xs text-slate-500">Motivo do cancelamento: {pedido.motivoCancelamento}</p>
              )}
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

export interface PropsDoFormularioDePedido {
  avaliacoes: Avaliacao[];
  /** Sessões registradas do animal, que também originam pedidos. */
  sessoes: SessaoRegistrada[];
  tratamentos: Tratamento[];
  /** Origem já definida pelo botão que abriu o formulário (cartão da avaliação ou registro da sessão). */
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

/** Pedido de exames a partir de uma avaliação ou de uma sessão registrada: emissão ou edição. */
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
          : 'O pedido fica no prontuário, ligado à origem e ao tratamento, e pode ser impresso para o tutor.'
      }
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <FormularioDePedido {...props} aoCancelar={aoFechar} />
    </Modal>
  );
}

function origemDoPedido(pedido: PedidoExame): string {
  return pedido.atendimentoId ? `sessao:${pedido.atendimentoId}` : `avaliacao:${pedido.avaliacaoId}`;
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

  const [origem, setOrigem] = useState(() => {
    if (pedido) return origemDoPedido(pedido);
    if (origemInicial) return valorDaOrigem(origemInicial);
    return avaliacoes.length === 1 && sessoes.length === 0 ? `avaliacao:${avaliacoes[0].id}` : '';
  });
  // Na origem avaliação, o tratamento é escolhido (com sugestão); na sessão, é o da própria sessão.
  const [tratamentoEscolhido, setTratamentoEscolhido] = useState(() =>
    pedido ? (pedido.tratamentoId ?? '') : tratamentoSugerido(tratamentos, origem),
  );
  const [exames, setExames] = useState(pedido?.exames ?? '');
  const [justificativa, setJustificativa] = useState(pedido?.justificativa ?? '');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const { avaliacaoId, atendimentoId } = lerOrigem(origem);
  const avaliacaoEscolhida = avaliacoes.find((a) => a.id === avaliacaoId);
  const sessaoDaOrigem = atendimentoId ? sessoes.find((s) => s.id === atendimentoId) : undefined;

  // O pedido de uma sessão não muda de origem; o de uma avaliação pode passar a outra avaliação.
  const origemTravada = !!origemInicial || !!pedido?.atendimentoId;

  function mudarOrigem(valor: string) {
    setOrigem(valor);
    setTratamentoEscolhido(tratamentoSugerido(tratamentos, valor));
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!origem) {
      setErro('Selecione a origem do pedido: a avaliação ou a sessão em que os exames foram pedidos.');
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

    // No pedido de uma sessão, a API liga o tratamento da própria sessão.
    const tratamentoId = atendimentoId ? null : tratamentoEscolhido || null;

    try {
      if (pedido) {
        await api.put(`/api/pedidos-exame/${pedido.id}`, {
          avaliacaoId: pedido.atendimentoId ? null : avaliacaoId,
          tratamentoId,
          exames: exames.trim(),
          justificativa: justificativa.trim(),
        });
        aoSalvar('Pedido de exame atualizado.');
      } else {
        await api.post('/api/pedidos-exame', {
          avaliacaoId,
          atendimentoId,
          tratamentoId,
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

  if (!origemTravada && !editando && avaliacoes.length === 0 && sessoes.length === 0) {
    return (
      <Alerta tipo="aviso">
        O pedido de exame é feito a partir de uma avaliação ou de uma sessão. Registre a avaliação do animal antes
        de pedir exames.
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
          : '';

  return (
    <form onSubmit={aoEnviar} className="space-y-4" noValidate>
      {origemTravada ? (
        <ResumoDaOrigem
          origem={descreverOrigem(origem, avaliacoes, sessoes) || pedido?.origem || ''}
          tratamento={atendimentoId ? sessaoDaOrigem?.nomeTratamento || pedido?.nomeTratamento : undefined}
        />
      ) : (
        <CampoOrigem
          valor={origem}
          aoMudar={mudarOrigem}
          avaliacoes={avaliacoes}
          // Um pedido de avaliação já emitido só troca por outra avaliação.
          sessoes={editando ? [] : sessoes}
          dica="A avaliação ou a sessão em que os exames foram pedidos."
        />
      )}

      {atendimentoId
        ? sessaoDaOrigem &&
          !origemTravada && (
            <Campo rotulo="Tratamento" dica="O pedido de uma sessão fica no tratamento da própria sessão.">
              <input className="vc-campo bg-slate-50" value={sessaoDaOrigem.nomeTratamento} readOnly />
            </Campo>
          )
        : (
            <CampoTratamento
              tratamentos={tratamentosParaVinculo(tratamentos, pedido?.tratamentoId)}
              valor={tratamentoEscolhido}
              aoMudar={setTratamentoEscolhido}
              tipoDoRegistro="o pedido"
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
