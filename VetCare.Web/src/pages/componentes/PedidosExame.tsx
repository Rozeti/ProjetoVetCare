import { useState, type FormEvent } from 'react';
import { Ban, FlaskConical, Loader2, Printer } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Avaliacao, PedidoExame } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { formatarData, resumirTexto } from '../../utils/formato';
import { imprimirPedidoExame } from '../../utils/impressao';

/** Mesmos limites de CriarPedidoExameDTO. */
const LIMITE_EXAMES = 2000;
const LIMITE_JUSTIFICATIVA = 2000;

interface ListaProps {
  pedidos: PedidoExame[];
  nomeClinica: string;
  /** Avaliações do animal: o pedido nasce de uma delas. */
  avaliacoes: Avaliacao[];
  /** Abre o formulário de pedido; ausente quando o prontuário é só consulta. */
  aoPedir?: () => void;
  aoAtualizar: (mensagem?: string) => void;
}

/** Pedidos de exame do animal, cada um ligado à avaliação que o originou. */
export function PedidosExame({ pedidos, nomeClinica, avaliacoes, aoPedir, aoAtualizar }: ListaProps) {
  const { temPerfil } = useAuth();
  const podeCancelar = temPerfil('Administrador', 'Veterinario');
  const { perguntar } = useConfirmacao();

  const [erro, setErro] = useState('');
  const [cancelando, setCancelando] = useState('');

  async function cancelar(pedido: PedidoExame) {
    const { confirmado, texto: motivo } = await perguntar({
      titulo: 'Cancelar pedido de exame',
      mensagem: (
        <>
          Cancelar o pedido emitido em <strong>{formatarData(pedido.dataEmissao)}</strong>? Ele continua no
          prontuário como cancelado e, se for impresso, sai marcado como sem validade.
        </>
      ),
      rotuloConfirmar: 'Cancelar pedido',
      rotuloCancelar: 'Voltar',
      perigo: true,
      campoTexto: { rotulo: 'Motivo do cancelamento', placeholder: 'Ex.: exame já realizado em outro laboratório' },
    });

    if (!confirmado) return;

    if (motivo.length > 300) {
      setErro('O motivo do cancelamento deve ter até 300 caracteres.');
      return;
    }

    setErro('');
    setCancelando(pedido.id);

    try {
      await api.patch(`/api/pedidos-exame/${pedido.id}/cancelar`, { motivo });
      aoAtualizar('Pedido de exame cancelado. O registro permanece no histórico do paciente.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível cancelar o pedido de exame.'));
    } finally {
      setCancelando('');
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
                    <Etiqueta
                      className={
                        pedido.status === 'Ativo' ? 'bg-sucesso-claro text-emerald-800' : 'bg-slate-200 text-slate-600'
                      }
                    >
                      {pedido.status}
                    </Etiqueta>
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

                  {podeCancelar && pedido.status === 'Ativo' && (
                    <button
                      type="button"
                      onClick={() => cancelar(pedido)}
                      disabled={cancelando === pedido.id}
                      className="rounded-lg p-2 text-slate-500 transition hover:bg-perigo-claro hover:text-perigo disabled:opacity-50"
                      title="Cancelar pedido"
                      aria-label={`Cancelar o pedido de ${formatarData(pedido.dataEmissao)}`}
                    >
                      {cancelando === pedido.id ? <Loader2 className="animate-spin" size={16} /> : <Ban size={16} />}
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
  aoFechar: () => void;
  aoSalvar: (mensagem: string) => void;
}

/** Pedido de exames: sempre a partir de uma avaliação do animal. */
export function ModalPedidoExame({ aberto, ...props }: ModalProps) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos novos.
  if (!aberto) return null;

  return <FormularioPedido {...props} />;
}

function FormularioPedido({ avaliacoes, avaliacaoId, aoFechar, aoSalvar }: Omit<ModalProps, 'aberto'>) {
  const { ehVeterinario } = useAuth();

  const [origem, setOrigem] = useState(avaliacaoId ?? (avaliacoes.length === 1 ? avaliacoes[0].id : ''));
  const [exames, setExames] = useState('');
  const [justificativa, setJustificativa] = useState('');
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

    try {
      await api.post('/api/pedidos-exame', {
        avaliacaoId: origem,
        exames: exames.trim(),
        justificativa: justificativa.trim(),
      });

      aoSalvar('Pedido de exame emitido com sucesso.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível emitir o pedido de exame.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal
      aberto
      titulo="Pedir exames"
      descricao="O pedido fica no prontuário, ligado à avaliação que o originou, e pode ser impresso para o tutor."
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <form onSubmit={aoEnviar} className="space-y-4" noValidate>
        <Campo rotulo="Avaliação de origem" obrigatorio>
          <select className="vc-campo" value={origem} onChange={(e) => setOrigem(e.target.value)} disabled={!!avaliacaoId}>
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

        {!ehVeterinario && avaliacaoEscolhida && (
          <p className="text-sm text-slate-600">
            Assinado por <strong>{avaliacaoEscolhida.nomeVeterinario}</strong>, responsável pela avaliação.
          </p>
        )}

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-1">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            Emitir pedido
          </button>
        </div>
      </form>
    </Modal>
  );
}
