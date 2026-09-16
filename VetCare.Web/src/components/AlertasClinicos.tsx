import { AlertTriangle, ShieldAlert } from 'lucide-react';
import type { AlergiaCondicao, Gravidade } from '../types';
import { Etiqueta } from './ui';

const ESTILO_GRAVIDADE: Record<Gravidade, string> = {
  Grave: 'bg-perigo-claro text-red-800 border-red-300',
  Moderada: 'bg-alerta-claro text-amber-800 border-amber-300',
  Leve: 'bg-slate-100 text-slate-700 border-slate-300',
};

const ROTULO_TIPO: Record<string, string> = {
  Alergia: 'Alergia',
  Comorbidade: 'Comorbidade',
  Restricao: 'Restrição',
  Cirurgia: 'Cirurgia',
};

/**
 * Alergias e comorbidades ativas do paciente. Aparecem no topo do prontuário
 * porque precisam ser lidas antes de qualquer conduta clínica — é informação de
 * segurança, não um detalhe de cadastro.
 */
export function AlertasClinicos({
  alertas,
  compacto = false,
}: {
  alertas: AlergiaCondicao[];
  compacto?: boolean;
}) {
  if (alertas.length === 0) {
    return null;
  }

  const temGrave = alertas.some((a) => a.gravidade === 'Grave');

  if (compacto) {
    return (
      <div className="flex flex-wrap gap-1">
        {alertas.map((alerta) => (
          <Etiqueta key={alerta.id} className={ESTILO_GRAVIDADE[alerta.gravidade]}>
            <AlertTriangle size={11} />
            {alerta.descricao.length > 28 ? `${alerta.descricao.slice(0, 28)}…` : alerta.descricao}
          </Etiqueta>
        ))}
      </div>
    );
  }

  return (
    <div
      className={`rounded-2xl border-2 p-4 ${
        temGrave ? 'border-perigo bg-perigo-claro/40' : 'border-amber-300 bg-alerta-claro/40'
      }`}
      role="alert"
    >
      <div className="mb-3 flex items-center gap-2">
        <ShieldAlert size={18} className={temGrave ? 'text-perigo' : 'text-alerta'} />
        <h2 className={`text-sm font-bold uppercase tracking-wide ${temGrave ? 'text-red-800' : 'text-amber-800'}`}>
          Atenção — {alertas.length} {alertas.length === 1 ? 'alerta clínico' : 'alertas clínicos'}
        </h2>
      </div>

      <ul className="space-y-2">
        {alertas.map((alerta) => (
          <li key={alerta.id} className="flex flex-wrap items-center gap-2">
            <Etiqueta className={ESTILO_GRAVIDADE[alerta.gravidade]}>{alerta.gravidade}</Etiqueta>
            <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {ROTULO_TIPO[alerta.tipo] ?? alerta.tipo}
            </span>
            <span className="text-sm font-medium text-slate-800">{alerta.descricao}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
