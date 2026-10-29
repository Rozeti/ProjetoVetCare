import { useState, type FormEvent } from 'react';
import { FlaskConical, Loader2, Pencil, Printer, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Avaliacao, PedidoExame } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { formatarData, resumirTexto } from '../../utils/formato';
import { imprimirPedidoExame } from '../../utils/impressao';

/** Mesmos limites de CriarPedidoExameDTO e AtualizarPedidoExameDTO. */
const LIMITE_EXAMES = 2000;
const LIMITE_JUSTIFICATIVA = 2000;

interface ListaProps {
  pedidos: PedidoExame[];
  nomeClinica: string;
  /** Avaliações do animal: o pedido nasce de uma delas. */
  avaliacoes: Avaliacao[];
  /** Abre o formulário de pedido; ausente quando o prontuário é só consulta. */
  aoPedir?: () => void;
  /** Abre o pedido para edição; ausente para quem não pode alterá-lo. */
  aoEditar?: (pedido: PedidoExame) => void;
  aoAtualizar: (mensagem?: string) => void;
}

/**
 * Pedidos de exame do animal, cada um ligado à avaliação que o originou. Quem assinou o pedido
 * (e a administração) pode editá-lo ou excluí-lo; a exclusão tira o pedido do prontuário e fica
 * registrada na auditoria.
 */
export function PedidosExame({ pedidos, nomeClinica, avaliacoes, aoPedir, aoEditar, aoAtualizar }: ListaProps) {
  const { temPerfil, ehVeterinario, usuario } = useAuth();
  const { confirmarExclusao } = useConfirmacao();

  const [erro, setErro] = useState('');
  const [excluindo, setExcluindo] = useState('');

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

        {aoPedir && avaliacoes.length > 0 && (
          <button type="button" className="vc-botao-primario" onClick={aoPedir}>
            <FlaskConical size={16} />
            Pedir exames
          </button>
        )}
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
            avaliacoes.length === 0
              ? 'Os pedidos de exame são emitidos a partir de uma avaliação. Registre a avaliação do animal primeiro.'
              : 'Os pedidos emitidos a partir das avaliações ficam aqui para consulta e impressão.'
          }
        />
      ) : (
        <ul className="divide-y divide-slate-100">
          {pedidos.map((pedido) => (
            <li key={pedido.id} className="px-5 py-4">
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
                    {pedido.dataAvaliacao && ` · Avaliação de ${formatarData(pedido.dataAvaliacao)}`}
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

interface ModalProps {
  aberto: boolean;
  avaliacoes: Avaliacao[];
  /** Avaliação já escolhida (pedido feito a partir do cartão da avaliação). */
  avaliacaoId?: string;
  /** Pedido em edição; ausente para um pedido novo. */
  pedido?: PedidoExame | null;
  aoFechar: () => void;
  aoSalvar: (mensagem: string) => void;
}

/** Pedido de exames, sempre a partir de uma avaliação do animal: emissão ou edição. */
export function ModalPedidoExame({ aberto, ...props }: ModalProps) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos novos.
  if (!aberto) return null;

  return <FormularioPedido {...props} />;
}

function FormularioPedido({ avaliacoes, avaliacaoId, pedido, aoFechar, aoSalvar }: Omit<ModalProps, 'aberto'>) {
  const { ehVeterinario } = useAuth();
  const editando = !!pedido;

  const [origem, setOrigem] = useState(
    pedido?.avaliacaoId ?? avaliacaoId ?? (avaliacoes.length === 1 ? avaliacoes[0].id : ''),
  );
  const [exames, setExames] = useState(pedido?.exames ?? '');
  const [justificativa, setJustificativa] = useState(pedido?.justificativa ?? '');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const avaliacaoEscolhida = avaliacoes.find((a) => a.id === origem);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!origem) {
      setErro('Selecione a avaliação que origina o pedido.');
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

    const corpo = {
      avaliacaoId: origem,
      exames: exames.trim(),
      justificativa: justificativa.trim(),
    };

    try {
      if (pedido) {
        await api.put(`/api/pedidos-exame/${pedido.id}`, corpo);
        aoSalvar('Pedido de exame atualizado.');
      } else {
        await api.post('/api/pedidos-exame', corpo);
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

  return (
    <Modal
      aberto
      titulo={editando ? 'Editar pedido de exame' : 'Pedir exames'}
      descricao={
        pedido
          ? `Pedido emitido em ${formatarData(pedido.dataEmissao)}. A versão anterior fica guardada no histórico.`
          : 'O pedido fica no prontuário, ligado à avaliação que o originou, e pode ser impresso para o tutor.'
      }
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <form onSubmit={aoEnviar} className="space-y-4" noValidate>
        <Campo rotulo="Avaliação de origem" obrigatorio>
          <select
            className="vc-campo"
            value={origem}
            onChange={(e) => setOrigem(e.target.value)}
            disabled={!editando && !!avaliacaoId}
          >
            <option value="">Selecione a avaliação</option>
            {avaliacoes.map((avaliacao) => (
              <option key={avaliacao.id} value={avaliacao.id}>
                {formatarData(avaliacao.dataRegistro)} — {resumirTexto(avaliacao.motivoConsulta, 70)}
              </option>
            ))}
          </select>
        </Campo>

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

        {pedido ? (
          <p className="text-sm text-slate-600">
            Assinado por <strong>{pedido.nomeVeterinario}</strong>.
          </p>
        ) : (
          !ehVeterinario &&
          avaliacaoEscolhida && (
            <p className="text-sm text-slate-600">
              Assinado por <strong>{avaliacaoEscolhida.nomeVeterinario}</strong>, responsável pela avaliação.
            </p>
          )
        )}

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-1">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            {editando ? 'Salvar alterações' : 'Emitir pedido'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
