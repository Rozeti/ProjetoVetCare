import { useCallback, useState, type FormEvent } from 'react';
import { CalendarOff, Loader2, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import type { BloqueioAgenda } from '../../types';
import { Alerta, Campo, Modal, SemDados } from '../../components/ui';
import { formatarDataHora, paraIsoLocal, paraValorInputData } from '../../utils/formato';
import { useCarregamento } from '../../hooks/useCarregamento';

interface Props {
  aberto: boolean;
  veterinarioId?: string;
  aoFechar: () => void;
  aoSalvar: () => void;
}

/**
 * Períodos de indisponibilidade do veterinário. Complementam a RN-002: além de
 * não haver outra sessão no horário, o profissional precisa estar disponível.
 */
export function ModalBloqueiosAgenda({ aberto, veterinarioId, aoFechar, aoSalvar }: Props) {
  // A lista só é buscada enquanto o modal está aberto: fechado, ele não consome a API.
  if (!aberto) {
    return null;
  }

  return <Conteudo veterinarioId={veterinarioId} aoFechar={aoFechar} aoSalvar={aoSalvar} />;
}

function Conteudo({ veterinarioId, aoFechar, aoSalvar }: Omit<Props, 'aberto'>) {
  const [dataInicio, setDataInicio] = useState(paraValorInputData(new Date()));
  const [horaInicio, setHoraInicio] = useState('08:00');
  const [dataFim, setDataFim] = useState(paraValorInputData(new Date()));
  const [horaFim, setHoraFim] = useState('18:00');
  const [motivo, setMotivo] = useState('');

  const [salvando, setSalvando] = useState(false);

  const buscar = useCallback(async () => {
    const { data } = await api.get<BloqueioAgenda[]>('/api/bloqueios-agenda', {
      params: { veterinarioId },
    });

    return data;
  }, [veterinarioId]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar os bloqueios.',
  );

  const bloqueios = dados ?? [];

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (motivo.trim().length < 3) {
      setErro('Informe o motivo do bloqueio.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/bloqueios-agenda', {
        veterinarioId: veterinarioId ?? null,
        inicio: paraIsoLocal(dataInicio, horaInicio),
        fim: paraIsoLocal(dataFim, horaFim),
        motivo,
      });

      setMotivo('');
      recarregar();
      aoSalvar();
    } catch (falha) {
      // Um período com sessões marcadas é recusado: o usuário precisa remarcá-las antes.
      setErro(mensagemDeErro(falha, 'Não foi possível criar o bloqueio.'));
    } finally {
      setSalvando(false);
    }
  }

  async function remover(bloqueio: BloqueioAgenda) {
    try {
      await api.delete(`/api/bloqueios-agenda/${bloqueio.id}`);
      recarregar();
      aoSalvar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível remover o bloqueio.'));
    }
  }

  return (
    <Modal
      aberto
      titulo="Bloqueios da agenda"
      descricao="Períodos de férias, congressos ou indisponibilidade não aceitam agendamento."
      aoFechar={aoFechar}
    >
      <form onSubmit={aoEnviar} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo rotulo="Início" obrigatorio>
            <div className="flex gap-2">
              <input
                type="date"
                className="vc-campo"
                value={dataInicio}
                onChange={(e) => setDataInicio(e.target.value)}
              />
              <input
                type="time"
                className="vc-campo w-32"
                value={horaInicio}
                onChange={(e) => setHoraInicio(e.target.value)}
              />
            </div>
          </Campo>

          <Campo rotulo="Fim" obrigatorio>
            <div className="flex gap-2">
              <input
                type="date"
                className="vc-campo"
                min={dataInicio}
                value={dataFim}
                onChange={(e) => setDataFim(e.target.value)}
              />
              <input
                type="time"
                className="vc-campo w-32"
                value={horaFim}
                onChange={(e) => setHoraFim(e.target.value)}
              />
            </div>
          </Campo>
        </div>

        <Campo rotulo="Motivo" obrigatorio>
          <input
            className="vc-campo"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="Ex.: férias, congresso, horário de almoço"
          />
        </Campo>

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end">
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            Bloquear período
          </button>
        </div>
      </form>

      <div className="mt-6 border-t border-slate-200 pt-5">
        <h3 className="mb-3 text-sm font-semibold text-slate-700">Bloqueios ativos</h3>

        {carregando ? (
          <p className="py-4 text-center text-sm text-slate-500">Carregando...</p>
        ) : bloqueios.length === 0 ? (
          <SemDados icone={<CalendarOff size={32} />} titulo="Nenhum período bloqueado" />
        ) : (
          <ul className="space-y-2">
            {bloqueios.map((bloqueio) => (
              <li
                key={bloqueio.id}
                className="flex items-center gap-3 rounded-xl border border-slate-200 px-4 py-3"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-slate-800">{bloqueio.motivo}</p>
                  <p className="text-xs text-slate-500">
                    {formatarDataHora(bloqueio.inicio)} — {formatarDataHora(bloqueio.fim)}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => remover(bloqueio)}
                  className="rounded-lg p-2 text-slate-400 transition hover:bg-perigo-claro hover:text-perigo"
                  title="Remover bloqueio"
                >
                  <Trash2 size={15} />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
