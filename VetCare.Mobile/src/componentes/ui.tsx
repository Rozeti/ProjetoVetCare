import type { ReactNode } from 'react';
import { ActivityIndicator, StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';
import { cores, espacos, raios, sombraCard } from '../tema';
import { iniciais } from '../utils/formato';

/** Peças visuais reutilizadas pelas telas do aplicativo do tutor. */

export function Cartao({ children, estilo }: { children: ReactNode; estilo?: StyleProp<ViewStyle> }) {
  return <View style={[estilos.cartao, estilo]}>{children}</View>;
}

export function Etiqueta({
  texto,
  fundo,
  cor,
}: {
  texto: string;
  fundo: string;
  cor: string;
}) {
  return (
    <View style={[estilos.etiqueta, { backgroundColor: fundo }]}>
      <Text style={[estilos.etiquetaTexto, { color: cor }]}>{texto}</Text>
    </View>
  );
}

export function Carregando({ texto = 'Carregando...' }: { texto?: string }) {
  return (
    <View style={estilos.centro}>
      <ActivityIndicator size="large" color={cores.marca} />
      <Text style={estilos.textoSuave}>{texto}</Text>
    </View>
  );
}

/**
 * Estado vazio explicativo. Vários critérios de aceite pedem que a ausência de
 * dados seja informada como conteúdo, e não como erro.
 */
export function SemDados({
  icone,
  titulo,
  descricao,
}: {
  icone?: string;
  titulo: string;
  descricao?: string;
}) {
  return (
    <View style={estilos.semDados}>
      <Text style={estilos.semDadosIcone}>{icone ?? 'ℹ'}</Text>
      <Text style={estilos.semDadosTitulo}>{titulo}</Text>
      {descricao ? <Text style={estilos.semDadosDescricao}>{descricao}</Text> : null}
    </View>
  );
}

export function Aviso({
  tipo = 'info',
  children,
}: {
  tipo?: 'info' | 'erro' | 'sucesso' | 'alerta';
  children: ReactNode;
}) {
  const paleta = {
    info: { fundo: cores.marcaClara, texto: cores.marcaEscura },
    erro: { fundo: cores.perigoClaro, texto: '#991b1b' },
    sucesso: { fundo: cores.sucessoClaro, texto: '#065f46' },
    alerta: { fundo: cores.alertaClaro, texto: '#92400e' },
  }[tipo];

  return (
    <View style={[estilos.aviso, { backgroundColor: paleta.fundo }]}>
      <Text style={[estilos.avisoTexto, { color: paleta.texto }]}>{children}</Text>
    </View>
  );
}

export function Avatar({ nome, tamanho = 40 }: { nome: string; tamanho?: number }) {
  return (
    <View
      style={[
        estilos.avatar,
        { width: tamanho, height: tamanho, borderRadius: tamanho / 2 },
      ]}
    >
      <Text style={[estilos.avatarTexto, { fontSize: tamanho * 0.36 }]}>{iniciais(nome)}</Text>
    </View>
  );
}

export function TituloSecao({ children, estilo }: { children: ReactNode; estilo?: StyleProp<ViewStyle> }) {
  return (
    <View style={[estilos.tituloSecao, estilo]}>
      <Text style={estilos.tituloSecaoTexto}>{children}</Text>
    </View>
  );
}

const estilos = StyleSheet.create({
  cartao: {
    backgroundColor: cores.superficie,
    borderRadius: raios.lg,
    borderWidth: 1,
    borderColor: cores.borda,
    padding: espacos.md,
    ...sombraCard,
  },
  etiqueta: {
    alignSelf: 'flex-start',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: raios.cheio,
  },
  etiquetaTexto: {
    fontSize: 12,
    fontWeight: '700',
  },
  centro: {
    paddingVertical: 48,
    alignItems: 'center',
    gap: espacos.sm,
  },
  textoSuave: {
    color: cores.textoSecundario,
    fontSize: 14,
  },
  semDados: {
    alignItems: 'center',
    paddingVertical: 48,
    paddingHorizontal: espacos.lg,
    gap: espacos.xs,
  },
  semDadosIcone: {
    fontSize: 36,
    color: cores.textoSuave,
    marginBottom: espacos.sm,
  },
  semDadosTitulo: {
    fontSize: 16,
    fontWeight: '700',
    color: cores.texto,
    textAlign: 'center',
  },
  semDadosDescricao: {
    fontSize: 14,
    color: cores.textoSecundario,
    textAlign: 'center',
    lineHeight: 20,
  },
  aviso: {
    borderRadius: raios.md,
    padding: espacos.md,
  },
  avisoTexto: {
    fontSize: 14,
    lineHeight: 20,
  },
  avatar: {
    backgroundColor: cores.marca,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarTexto: {
    color: '#ffffff',
    fontWeight: '700',
  },
  tituloSecao: {
    marginTop: espacos.lg,
    marginBottom: espacos.sm,
  },
  tituloSecaoTexto: {
    fontSize: 17,
    fontWeight: '700',
    color: cores.texto,
  },
});
