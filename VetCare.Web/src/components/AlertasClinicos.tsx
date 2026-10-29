import { AlertTriangle, Syringe } from 'lucide-react';
import type { AlergiaCondicao, Vacina } from '../types';
import { Etiqueta } from './ui';
import { estiloGravidade, formatarDia, rotuloTipoAlerta } from '../utils/formato';

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
 * informação de segurança, não um detalhe de cadastro. Cada item mostra só o nome e a
 * descrição, na cor da gravidade, lado a lado para que a leitura seja rápida.
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

  // Sem título nem moldura: cada alerta é só o nome e a descrição, na cor da gravidade.
  return (
    <ul className="flex flex-wrap gap-2" role="alert" aria-label="Alertas clínicos">
      {alertas.map((alerta) => (
        <li
          key={alerta.id}
          className={`flex min-w-0 max-w-full flex-1 basis-64 items-start gap-2 rounded-xl px-3 py-2 ${COR_DA_GRAVIDADE[alerta.gravidade]}`}
          title={`Gravidade ${alerta.gravidade.toLowerCase()}`}
        >
          <AlertTriangle size={15} className="mt-0.5 shrink-0" />
          <p className="min-w-0 text-sm">
            <span className="font-semibold">{rotuloTipoAlerta[alerta.tipo] ?? alerta.tipo}:</span> {alerta.descricao}
          </p>
        </li>
      ))}

      {/* Prevenção atrasada fica junto das demais condições; o item abre a carteira de vacinação. */}
      {dosesVencidas.map((dose) => {
        const texto = (
          <>
            <span className="font-semibold">Vacinação:</span> {dose.nome}
            {dose.descricaoDose && ` (${dose.descricaoDose.toLowerCase()})`}
            {dose.proximaDose && ` — prevista para ${formatarDia(dose.proximaDose)}`}
            {dose.diasParaProximaDose != null &&
              dose.diasParaProximaDose < 0 &&
              `, ${Math.abs(dose.diasParaProximaDose)} dia(s) em atraso`}
          </>
        );

        return (
          <li
            key={dose.id}
            className={`flex min-w-0 max-w-full flex-1 basis-64 items-start gap-2 rounded-xl px-3 py-2 ${COR_DA_GRAVIDADE.Moderada}`}
          >
            <Syringe size={15} className="mt-0.5 shrink-0" />
            {aoVerVacinas ? (
              <button type="button" onClick={aoVerVacinas} className="min-w-0 text-left text-sm hover:underline">
                {texto}
              </button>
            ) : (
              <p className="min-w-0 text-sm">{texto}</p>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** Cor característica de cada gravidade, sem borda. */
const COR_DA_GRAVIDADE: Record<AlergiaCondicao['gravidade'], string> = {
  Grave: 'bg-perigo-claro text-red-800',
  Moderada: 'bg-alerta-claro text-amber-800',
  Leve: 'bg-slate-100 text-slate-700',
};
