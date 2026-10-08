import { AlertTriangle, ShieldAlert, Syringe } from 'lucide-react';
import type { AlergiaCondicao, Vacina } from '../types';
import { Etiqueta } from './ui';
import { estiloGravidade, formatarData, rotuloTipoAlerta } from '../utils/formato';

const ESTILO_DOSE_ATRASADA = 'bg-alerta-claro text-amber-800 border-amber-300';

interface Props {
  alertas: AlergiaCondicao[];
  /** Doses da carteira já vencidas: entram como alerta ao lado das condições do paciente. */
  dosesVencidas?: Vacina[];
  /** Só as etiquetas, para listas. Neste modo a contagem de doses vencidas vem da API. */
  compacto?: boolean;
  vacinasVencidas?: number;
  /** Atalho para a aba de vacinação, quando a tela tem uma. */
  aoVerVacinas?: () => void;
}

/**
 * Alergias, comorbidades e vacinação em atraso do paciente. Aparecem no topo do
 * prontuário porque precisam ser lidas antes de qualquer conduta clínica — é
 * informação de segurança, não um detalhe de cadastro. Os itens ficam lado a lado
 * para que a leitura seja rápida e a caixa não empurre o restante da tela.
 */
export function AlertasClinicos({
  alertas,
  dosesVencidas = [],
  compacto = false,
  vacinasVencidas = 0,
  aoVerVacinas,
}: Props) {
  const totalDeDosesVencidas = compacto ? vacinasVencidas : dosesVencidas.length;

  if (alertas.length === 0 && totalDeDosesVencidas === 0) {
    return null;
  }

  const temGrave = alertas.some((a) => a.gravidade === 'Grave');

  if (compacto) {
    return (
      <div className="flex flex-wrap gap-1">
        {alertas.map((alerta) => (
          <Etiqueta key={alerta.id} className={estiloGravidade[alerta.gravidade]}>
            <AlertTriangle size={11} />
            {alerta.descricao.length > 28 ? `${alerta.descricao.slice(0, 28)}…` : alerta.descricao}
          </Etiqueta>
        ))}

        {vacinasVencidas > 0 && (
          <Etiqueta className={ESTILO_DOSE_ATRASADA}>
            <Syringe size={11} />
            {vacinasVencidas === 1 ? 'Vacina em atraso' : `${vacinasVencidas} vacinas em atraso`}
          </Etiqueta>
        )}
      </div>
    );
  }

  const total = alertas.length + dosesVencidas.length;

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
          Atenção — {total} {total === 1 ? 'alerta clínico' : 'alertas clínicos'}
        </h2>
      </div>

      <ul className="flex flex-wrap gap-2">
        {alertas.map((alerta) => (
          <li
            key={alerta.id}
            className="flex min-w-0 max-w-full flex-1 basis-64 items-start gap-2 rounded-xl border border-white/70 bg-white/70 px-3 py-2"
          >
            <Etiqueta className={`shrink-0 ${estiloGravidade[alerta.gravidade]}`}>{alerta.gravidade}</Etiqueta>
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                {rotuloTipoAlerta[alerta.tipo] ?? alerta.tipo}
              </p>
              <p className="text-sm font-medium text-slate-800">{alerta.descricao}</p>
            </div>
          </li>
        ))}

        {/* Prevenção atrasada é condição relacionada ao atendimento: fica junto das demais. */}
        {dosesVencidas.map((dose) => (
          <li
            key={dose.id}
            className="flex min-w-0 max-w-full flex-1 basis-64 items-start gap-2 rounded-xl border border-white/70 bg-white/70 px-3 py-2"
          >
            <Etiqueta className={`shrink-0 ${ESTILO_DOSE_ATRASADA}`}>
              <Syringe size={11} />
              Atrasada
            </Etiqueta>
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Vacinação</p>
              <p className="text-sm font-medium text-slate-800">
                {dose.nome}
                {dose.descricaoDose && ` (${dose.descricaoDose.toLowerCase()})`}
                {dose.proximaDose && ` — prevista para ${formatarData(dose.proximaDose)}`}
                {dose.diasParaProximaDose != null &&
                  dose.diasParaProximaDose < 0 &&
                  `, ${Math.abs(dose.diasParaProximaDose)} dia(s) em atraso`}
              </p>
            </div>
          </li>
        ))}
      </ul>

      {dosesVencidas.length > 0 && aoVerVacinas && (
        <button type="button" onClick={aoVerVacinas} className="mt-3 text-xs font-semibold text-brand hover:underline">
          Abrir a carteira de vacinação
        </button>
      )}
    </div>
  );
}
