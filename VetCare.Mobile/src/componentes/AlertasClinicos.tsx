import { StyleSheet, Text, View } from 'react-native';
import { cores, espacos, raios } from '../tema';
import type { AlergiaCondicao, Gravidade, Vacina } from '../tipos';
import { formatarData } from '../utils/formato';

const PALETA: Record<Gravidade, { fundo: string; borda: string; texto: string }> = {
  Grave: { fundo: cores.perigoClaro, borda: cores.perigo, texto: '#991b1b' },
  Moderada: { fundo: cores.alertaClaro, borda: cores.alerta, texto: '#92400e' },
  Leve: { fundo: cores.fundo, borda: cores.borda, texto: cores.textoSecundario },
};

const ORDEM: Gravidade[] = ['Grave', 'Moderada', 'Leve'];

const ROTULO_TIPO: Record<string, string> = {
  Alergia: 'Alergia',
  Comorbidade: 'Comorbidade',
  Restricao: 'Restrição',
  Cirurgia: 'Cirurgia',
};

interface Props {
  alertas: AlergiaCondicao[];
  /** Doses da carteira já vencidas, exibidas junto das condições do pet. */
  dosesVencidas?: Vacina[];
}

/**
 * Alergias, comorbidades e vacinação em atraso do pet. O tutor vê estes alertas —
 * diferente das observações internas, é informação de segurança que ele precisa
 * conhecer. A caixa segue a cor do alerta mais grave e os itens ficam lado a lado,
 * como no portal.
 */
export function AlertasClinicos({ alertas, dosesVencidas = [] }: Props) {
  const total = alertas.length + dosesVencidas.length;

  if (total === 0) {
    return null;
  }

  const maisGrave =
    ORDEM.find((gravidade) => alertas.some((a) => a.gravidade === gravidade)) ??
    (dosesVencidas.length > 0 ? 'Moderada' : 'Leve');
  const paletaDaCaixa = PALETA[maisGrave];

  return (
    <View style={[estilos.caixa, { backgroundColor: paletaDaCaixa.fundo, borderColor: paletaDaCaixa.borda }]}>
      <Text style={[estilos.titulo, { color: paletaDaCaixa.texto }]}>
        ⚠ {total === 1 ? 'Alerta clínico' : `${total} alertas clínicos`}
      </Text>

      <View style={estilos.lista}>
        {alertas.map((alerta) => {
          const paleta = PALETA[alerta.gravidade] ?? PALETA.Leve;

          return (
            <View key={alerta.id} style={[estilos.item, { borderColor: paleta.borda }]}>
              <Text style={[estilos.gravidade, { color: paleta.texto }]}>
                {alerta.gravidade} · {ROTULO_TIPO[alerta.tipo] ?? alerta.tipo}
              </Text>
              <Text style={estilos.descricao}>{alerta.descricao}</Text>
            </View>
          );
        })}

        {dosesVencidas.map((dose) => (
          <View key={dose.id} style={[estilos.item, { borderColor: cores.alerta }]}>
            <Text style={[estilos.gravidade, { color: '#92400e' }]}>Atrasada · Vacinação</Text>
            <Text style={estilos.descricao}>
              {dose.nome}
              {dose.descricaoDose ? ` (${dose.descricaoDose.toLowerCase()})` : ''}
              {dose.proximaDose ? ` — prevista para ${formatarData(dose.proximaDose)}` : ''}
              {dose.diasParaProximaDose != null && dose.diasParaProximaDose < 0
                ? `, ${Math.abs(dose.diasParaProximaDose)} dia(s) em atraso`
                : ''}
            </Text>
          </View>
        ))}
      </View>
    </View>
  );
}

const estilos = StyleSheet.create({
  caixa: {
    borderWidth: 2,
    borderRadius: raios.lg,
    padding: espacos.md,
    gap: espacos.sm,
  },
  titulo: {
    fontSize: 13,
    fontWeight: '800',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  lista: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: espacos.sm,
  },
  item: {
    flexGrow: 1,
    flexBasis: '46%',
    minWidth: 140,
    backgroundColor: 'rgba(255, 255, 255, 0.75)',
    borderWidth: 1,
    borderRadius: raios.md,
    padding: espacos.sm,
    gap: 2,
  },
  gravidade: {
    fontSize: 11,
    fontWeight: '700',
    textTransform: 'uppercase',
  },
  descricao: {
    fontSize: 14,
    color: cores.texto,
    lineHeight: 20,
  },
});
