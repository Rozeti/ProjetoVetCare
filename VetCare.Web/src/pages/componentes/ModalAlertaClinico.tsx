import { useState, type FormEvent } from 'react';
import { Archive, Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import type { AlergiaCondicao, Gravidade, TipoAlerta } from '../../types';
import { Alerta, Campo, Etiqueta, Modal } from '../../components/ui';
import { formatarData } from '../../utils/formato';

const TIPOS: { valor: TipoAlerta; rotulo: string }[] = [
  { valor: 'Alergia', rotulo: 'Alergia' },
  { valor: 'Comorbidade', rotulo: 'Comorbidade' },
  { valor: 'Restricao', rotulo: 'Restrição' },
  { valor: 'Cirurgia', rotulo: 'Cirurgia anterior' },
];

const GRAVIDADES: Gravidade[] = ['Leve', 'Moderada', 'Grave'];

const ESTILO_GRAVIDADE: Record<Gravidade, string> = {
  Grave: 'bg-perigo-claro text-red-800',
  Moderada: 'bg-alerta-claro text-amber-800',
  Leve: 'bg-slate-100 text-slate-700',
};

interface Props {
  aberto: boolean;
  pacienteId: string;
  alertas: AlergiaCondicao[];
  aoFechar: () => void;
  aoSalvar: () => void;
}

/** Registro de alergias, comorbidades e restrições do paciente. */
export function ModalAlertaClinico({ aberto, pacienteId, alertas, aoFechar, aoSalvar }: Props) {
  const [tipo, setTipo] = useState<TipoAlerta>('Alergia');
  const [descricao, setDescricao] = useState('');
  const [gravidade, setGravidade] = useState<Gravidade>('Moderada');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (descricao.trim().length < 3) {
      setErro('Descreva o alerta clínico.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/alergias', { pacienteId, tipo, descricao, gravidade });

      setDescricao('');
      aoSalvar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível registrar o alerta.'));
    } finally {
      setSalvando(false);
    }
  }

  /** Condição resolvida é arquivada, não excluída: o histórico continua relevante. */
  async function arquivar(alerta: AlergiaCondicao) {
    try {
      await api.patch(`/api/alergias/${alerta.id}/status`, { ativo: false });
      aoSalvar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível arquivar o alerta.'));
    }
  }

  return (
    <Modal
      aberto={aberto}
      titulo="Alertas clínicos"
      descricao="Alergias e comorbidades aparecem em destaque no topo do prontuário."
      aoFechar={aoFechar}
    >
      <form onSubmit={aoEnviar} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo rotulo="Tipo" obrigatorio>
            <select className="vc-campo" value={tipo} onChange={(e) => setTipo(e.target.value as TipoAlerta)}>
              {TIPOS.map((item) => (
                <option key={item.valor} value={item.valor}>
                  {item.rotulo}
                </option>
              ))}
            </select>
          </Campo>

          <Campo rotulo="Gravidade" obrigatorio>
            <select
              className="vc-campo"
              value={gravidade}
              onChange={(e) => setGravidade(e.target.value as Gravidade)}
            >
              {GRAVIDADES.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
          </Campo>
        </div>

        <Campo rotulo="Descrição" obrigatorio>
          <textarea
            className="vc-campo"
            rows={2}
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
            placeholder="Ex.: alergia a dipirona, com histórico de reação cutânea."
          />
        </Campo>

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end">
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            Registrar alerta
          </button>
        </div>
      </form>

      {alertas.length > 0 && (
        <div className="mt-6 border-t border-slate-200 pt-5">
          <h3 className="mb-3 text-sm font-semibold text-slate-700">Alertas ativos</h3>

          <ul className="space-y-2">
            {alertas.map((alerta) => (
              <li
                key={alerta.id}
                className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 px-4 py-3"
              >
                <Etiqueta className={ESTILO_GRAVIDADE[alerta.gravidade]}>{alerta.gravidade}</Etiqueta>

                <div className="min-w-0 flex-1">
                  <p className="text-sm text-slate-800">{alerta.descricao}</p>
                  <p className="text-xs text-slate-400">
                    {alerta.tipo} · {alerta.registradoPor} · {formatarData(alerta.dataRegistro)}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => arquivar(alerta)}
                  className="rounded-lg p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
                  title="Arquivar alerta"
                >
                  <Archive size={15} />
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Modal>
  );
}
