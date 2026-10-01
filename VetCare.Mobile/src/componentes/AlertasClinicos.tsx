import { StyleSheet, Text, View } from 'react-native';
import { cores, espacos, raios } from '../tema';
import type { AlergiaCondicao, Gravidade } from '../tipos';

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

/**
 * Alergias e comorbidades do pet. O tutor vê estes alertas — diferente das
 * observações internas, é informação de segurança que ele precisa conhecer.
 * A caixa segue a cor do alerta mais grave; cada item mostra a própria gravidade.
 */
export function AlertasClinicos({ alertas }: { alertas: AlergiaCondicao[] }) {
  if (alertas.length === 0) {
    return null;
  }

  const maisGrave = ORDEM.find((gravidade) => alertas.some((a) => a.gravidade === gravidade)) ?? 'Leve';
  const paletaDaCaixa = PALETA[maisGrave];

  return (
    <View style={[estilos.caixa, { backgroundColor: paletaDaCaixa.fundo, borderColor: paletaDaCaixa.borda }]}>
      <Text style={[estilos.titulo, { color: paletaDaCaixa.texto }]}>
        ⚠ {alertas.length === 1 ? 'Alerta clínico' : `${alertas.length} alertas clínicos`}
      </Text>

      {alertas.map((alerta) => {
        const paleta = PALETA[alerta.gravidade] ?? PALETA.Leve;

        return (
          <View key={alerta.id} style={estilos.item}>
            <Text style={[estilos.gravidade, { color: paleta.texto }]}>
              {alerta.gravidade} · {ROTULO_TIPO[alerta.tipo] ?? alerta.tipo}
            </Text>
            <Text style={estilos.descricao}>{alerta.descricao}</Text>
          </View>
        );
      })}
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
  item: {
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
