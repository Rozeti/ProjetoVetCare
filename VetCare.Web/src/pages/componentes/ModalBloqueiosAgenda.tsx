import { useCallback, useId, useState, type FormEvent } from 'react';
import { ArrowRight, CalendarOff, CalendarPlus, Loader2, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import type { BloqueioAgenda } from '../../types';
import { Alerta, Campo, Carregando, Etiqueta, Modal, SemDados } from '../../components/ui';
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
  const [removendo, setRemovendo] = useState<string | null>(null);
  const [aviso, setAviso] = useState('');

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
    setAviso('');

    if (motivo.trim().length < 3) {
      setErro('Informe o motivo do bloqueio.');
      return;
    }

    const inicio = paraIsoLocal(dataInicio, horaInicio);
    const fim = paraIsoLocal(dataFim, horaFim);

    if (fim <= inicio) {
      setErro('O fim do bloqueio precisa ser depois do início.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/bloqueios-agenda', {
        veterinarioId: veterinarioId ?? null,
        inicio,
        fim,
        motivo,
      });

      setMotivo('');
      setAviso('Período bloqueado. Nenhuma sessão poderá ser marcada nesse intervalo.');
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
    setErro('');
    setAviso('');
    setRemovendo(bloqueio.id);

    try {
      await api.delete(`/api/bloqueios-agenda/${bloqueio.id}`);
      recarregar();
      aoSalvar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível remover o bloqueio.'));
    } finally {
      setRemovendo(null);
    }
  }

  return (
    <Modal
      aberto
      titulo="Bloqueios da agenda"
      descricao="Períodos de férias, congressos ou indisponibilidade não aceitam agendamento."
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <form onSubmit={aoEnviar} className="rounded-2xl border border-slate-200 bg-slate-50/70 p-3 sm:p-5">
        <div className="mb-4 flex items-center gap-2.5">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-brand-dark">
            <CalendarPlus size={16} />
          </span>
          <h3 className="text-sm font-semibold text-slate-800">Novo bloqueio</h3>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <CampoPeriodo
            rotulo="Início"
            data={dataInicio}
            hora={horaInicio}
            aoMudarData={(valor) => {
              setDataInicio(valor);
              // O fim nunca fica antes do início: acompanha a data quando ela avança.
              if (valor > dataFim) setDataFim(valor);
            }}
            aoMudarHora={setHoraInicio}
          />

          <CampoPeriodo
            rotulo="Fim"
            data={dataFim}
            hora={horaFim}
            dataMinima={dataInicio}
            aoMudarData={setDataFim}
            aoMudarHora={setHoraFim}
          />
        </div>

        <div className="mt-4">
          <Campo rotulo="Motivo" obrigatorio>
            <input
              className="vc-campo"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              placeholder="Ex.: férias, congresso, horário de almoço"
              maxLength={120}
            />
          </Campo>
        </div>

        {erro && (
          <div className="mt-4">
            <Alerta tipo="erro">{erro}</Alerta>
          </div>
        )}

        {aviso && (
          <div className="mt-4">
            <Alerta tipo="sucesso" aoFechar={() => setAviso('')}>
              {aviso}
            </Alerta>
          </div>
        )}

        <div className="mt-4 flex justify-end">
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando ? <Loader2 className="animate-spin" size={16} /> : <CalendarOff size={16} />}
            Bloquear período
          </button>
        </div>
      </form>

      <section className="mt-6" aria-label="Bloqueios ativos">
        <div className="mb-3 flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-slate-800">Bloqueios ativos</h3>
          {bloqueios.length > 0 && (
            <Etiqueta className="bg-slate-100 text-slate-600">
              {bloqueios.length} {bloqueios.length === 1 ? 'período' : 'períodos'}
            </Etiqueta>
          )}
        </div>

        {carregando ? (
          <Carregando texto="Carregando bloqueios..." />
        ) : bloqueios.length === 0 ? (
          <SemDados
            compacto
            icone={<CalendarOff size={28} />}
            titulo="Nenhum período bloqueado"
            descricao="A agenda está aberta para agendamentos em todos os horários de expediente."
          />
        ) : (
          <ul className="max-h-72 space-y-2 overflow-y-auto pr-1">
            {bloqueios.map((bloqueio) => (
              <li
                key={bloqueio.id}
                className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white px-3 py-3 sm:px-4"
              >
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-alerta-claro text-amber-700">
                  <CalendarOff size={16} />
                </span>

                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-slate-800">{bloqueio.motivo}</p>
                  <p className="mt-0.5 flex flex-wrap items-center gap-x-1.5 text-xs text-slate-500">
                    <span>{formatarDataHora(bloqueio.inicio)}</span>
                    <ArrowRight size={12} className="shrink-0 text-slate-400" aria-hidden="true" />
                    <span>{formatarDataHora(bloqueio.fim)}</span>
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => remover(bloqueio)}
                  disabled={removendo === bloqueio.id}
                  className="shrink-0 rounded-lg p-2 text-slate-400 transition hover:bg-perigo-claro hover:text-perigo disabled:opacity-60"
                  title="Remover bloqueio"
                  aria-label={`Remover bloqueio: ${bloqueio.motivo}`}
                >
                  {removendo === bloqueio.id ? (
                    <Loader2 className="animate-spin" size={15} />
                  ) : (
                    <Trash2 size={15} />
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </Modal>
  );
}

/**
 * Data e hora lado a lado, com rótulo único ("Início", "Fim"). A coluna da data pode
 * encolher (minmax(0, 1fr)) e a da hora tem largura fixa: é o que impede os dois campos
 * de empurrarem um ao outro para fora do modal em telas estreitas.
 */
function CampoPeriodo({
  rotulo,
  data,
  hora,
  dataMinima,
  aoMudarData,
  aoMudarHora,
}: {
  rotulo: string;
  data: string;
  hora: string;
  dataMinima?: string;
  aoMudarData: (valor: string) => void;
  aoMudarHora: (valor: string) => void;
}) {
  const idRotulo = useId();
  const nome = rotulo.toLowerCase();

  return (
    <div className="min-w-0" role="group" aria-labelledby={idRotulo}>
      <span id={idRotulo} className="vc-rotulo">
        {rotulo}
        <span className="ml-0.5 text-perigo">*</span>
      </span>

      <div className="grid grid-cols-[minmax(0,1fr)_6.5rem] gap-2">
        <input
          type="date"
          className="vc-campo min-w-0 px-3"
          aria-label={`Data de ${nome}`}
          min={dataMinima}
          value={data}
          onChange={(e) => aoMudarData(e.target.value)}
          required
        />
        <input
          type="time"
          className="vc-campo min-w-0 px-2 text-center"
          aria-label={`Hora de ${nome}`}
          value={hora}
          onChange={(e) => aoMudarHora(e.target.value)}
          required
        />
      </div>
    </div>
  );
}
