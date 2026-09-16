import { StyleSheet, Text, View } from 'react-native';
import { cores, espacos, raios } from '../tema';
import type { PontoEvolucao } from '../tipos';
import { formatarData } from '../utils/formato';

interface Props {
  titulo: string;
  pontos: PontoEvolucao[];
  unidade?: string;
  cor?: string;
  /** Fixa a escala, usado pela escala de dor que sempre vai de 0 a 10. */
  minimoFixo?: number;
  maximoFixo?: number;
}

const ALTURA = 120;

/**
 * HU-011, CA-3: evolução de peso e de dor no aplicativo do tutor.
 * Desenhado com Views em vez de SVG para não exigir react-native-svg, que
 * adicionaria dependência nativa ao projeto.
 */
export function GraficoBarras({ titulo, pontos, unidade = '', cor = cores.marca, minimoFixo, maximoFixo }: Props) {
  const formatar = (valor: number) => `${valor.toFixed(1).replace('.', ',')}${unidade ? ` ${unidade}` : ''}`;

  if (pontos.length === 0) {
    return (
      <View>
        <Text style={estilos.titulo}>{titulo}</Text>
        <Text style={estilos.vazio}>Ainda não há registros suficientes para montar este gráfico.</Text>
      </View>
    );
  }

  // Mostramos no máximo os 8 registros mais recentes: além disso as barras
  // ficariam estreitas demais na largura de um celular.
  const visiveis = pontos.slice(-8);
  const valores = visiveis.map((p) => p.valor);

  const minimoBruto = minimoFixo ?? Math.min(...valores);
  const maximoBruto = maximoFixo ?? Math.max(...valores);
  const folga = maximoBruto === minimoBruto ? Math.max(1, maximoBruto * 0.1) : (maximoBruto - minimoBruto) * 0.2;

  const minimo = minimoFixo ?? minimoBruto - folga;
  const maximo = maximoFixo ?? maximoBruto + folga;

  const ultimo = pontos[pontos.length - 1];

  return (
    <View>
      <View style={estilos.cabecalho}>
        <Text style={estilos.titulo}>{titulo}</Text>
        <Text style={estilos.atual}>
          Atual: <Text style={estilos.atualValor}>{formatar(ultimo.valor)}</Text>
        </Text>
      </View>

      <View style={estilos.area}>
        {visiveis.map((ponto, indice) => {
          const proporcao = (ponto.valor - minimo) / (maximo - minimo || 1);
          const altura = Math.max(6, proporcao * ALTURA);

          return (
            <View key={`${ponto.data}-${indice}`} style={estilos.coluna}>
              <Text style={estilos.valor}>{formatar(ponto.valor)}</Text>

              <View style={estilos.trilha}>
                <View style={[estilos.barra, { height: altura, backgroundColor: cor }]} />
              </View>

              <Text style={estilos.rotulo}>{formatarData(ponto.data).slice(0, 5)}</Text>
            </View>
          );
        })}
      </View>

      {pontos.length > visiveis.length && (
        <Text style={estilos.nota}>Exibindo os {visiveis.length} registros mais recentes.</Text>
      )}
    </View>
  );
}

const estilos = StyleSheet.create({
  cabecalho: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    marginBottom: espacos.md,
    gap: espacos.sm,
  },
  titulo: {
    fontSize: 15,
    fontWeight: '700',
    color: cores.texto,
  },
  atual: {
    fontSize: 12,
    color: cores.textoSecundario,
  },
  atualValor: {
    fontWeight: '700',
    color: cores.texto,
  },
  area: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    justifyContent: 'space-around',
    gap: espacos.xs,
  },
  coluna: {
    flex: 1,
    alignItems: 'center',
    gap: 4,
  },
  trilha: {
    height: ALTURA,
    width: '100%',
    justifyContent: 'flex-end',
    alignItems: 'center',
  },
  barra: {
    width: '70%',
    borderTopLeftRadius: raios.sm,
    borderTopRightRadius: raios.sm,
    minWidth: 10,
  },
  valor: {
    fontSize: 10,
    color: cores.textoSecundario,
    fontWeight: '600',
  },
  rotulo: {
    fontSize: 10,
    color: cores.textoSuave,
  },
  vazio: {
    marginTop: espacos.sm,
    backgroundColor: cores.fundo,
    borderRadius: raios.md,
    padding: espacos.md,
    fontSize: 13,
    color: cores.textoSecundario,
    textAlign: 'center',
  },
  nota: {
    marginTop: espacos.sm,
    fontSize: 11,
    color: cores.textoSuave,
    textAlign: 'center',
  },
});
