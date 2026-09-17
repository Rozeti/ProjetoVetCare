import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
  type KeyboardTypeOptions,
  type StyleProp,
  type ViewStyle,
} from 'react-native';
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

/** Rótulo e campo de texto do formulário, com o mesmo desenho dos cartões. */
export function CampoTexto({
  rotulo,
  valor,
  aoMudar,
  obrigatorio,
  dica,
  exemplo,
  teclado = 'default',
  maximo,
}: {
  rotulo: string;
  valor: string;
  aoMudar: (texto: string) => void;
  obrigatorio?: boolean;
  dica?: string;
  exemplo?: string;
  teclado?: KeyboardTypeOptions;
  maximo?: number;
}) {
  return (
    <View style={estilos.campo}>
      <Text style={estilos.rotulo}>
        {rotulo}
        {obrigatorio ? <Text style={estilos.obrigatorio}> *</Text> : null}
      </Text>

      <TextInput
        style={estilos.entrada}
        value={valor}
        onChangeText={aoMudar}
        placeholder={exemplo}
        placeholderTextColor={cores.textoSuave}
        keyboardType={teclado}
        maxLength={maximo}
        accessibilityLabel={rotulo}
      />

      {dica ? <Text style={estilos.dica}>{dica}</Text> : null}
    </View>
  );
}

/**
 * Escolha entre poucas opções. Botões lado a lado evitam a lista suspensa, que no
 * celular exige um toque a mais e esconde as alternativas.
 */
export function SeletorDeOpcao({
  rotulo,
  opcoes,
  selecionada,
  aoSelecionar,
  obrigatorio,
}: {
  rotulo: string;
  opcoes: readonly string[];
  selecionada: string;
  aoSelecionar: (opcao: string) => void;
  obrigatorio?: boolean;
}) {
  return (
    <View style={estilos.campo}>
      <Text style={estilos.rotulo}>
        {rotulo}
        {obrigatorio ? <Text style={estilos.obrigatorio}> *</Text> : null}
      </Text>

      <View style={estilos.opcoes}>
        {opcoes.map((opcao) => {
          const ativa = opcao === selecionada;

          return (
            <TouchableOpacity
              key={opcao}
              style={[estilos.opcao, ativa && estilos.opcaoAtiva]}
              onPress={() => aoSelecionar(opcao)}
              accessibilityRole="radio"
              accessibilityState={{ selected: ativa }}
            >
              <Text style={[estilos.opcaoTexto, ativa && estilos.opcaoTextoAtivo]}>{opcao}</Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );
}

/** Caixa de marcação simples, para os campos de sim ou não. */
export function CampoDeMarcacao({
  rotulo,
  marcado,
  aoAlternar,
}: {
  rotulo: string;
  marcado: boolean;
  aoAlternar: (valor: boolean) => void;
}) {
  return (
    <TouchableOpacity
      style={estilos.marcacao}
      onPress={() => aoAlternar(!marcado)}
      accessibilityRole="checkbox"
      accessibilityState={{ checked: marcado }}
      accessibilityLabel={rotulo}
    >
      <View style={[estilos.caixa, marcado && estilos.caixaMarcada]}>
        {marcado ? <Text style={estilos.caixaTexto}>✓</Text> : null}
      </View>
      <Text style={estilos.marcacaoTexto}>{rotulo}</Text>
    </TouchableOpacity>
  );
}

export function Botao({
  titulo,
  aoPressionar,
  variante = 'primario',
  desabilitado,
  carregando,
  estilo,
}: {
  titulo: string;
  aoPressionar: () => void;
  variante?: 'primario' | 'secundario';
  desabilitado?: boolean;
  carregando?: boolean;
  estilo?: StyleProp<ViewStyle>;
}) {
  const ehPrimario = variante === 'primario';
  const inativo = desabilitado || carregando;

  return (
    <TouchableOpacity
      style={[
        estilos.botao,
        ehPrimario ? estilos.botaoPrimario : estilos.botaoSecundario,
        inativo && estilos.botaoInativo,
        estilo,
      ]}
      onPress={aoPressionar}
      disabled={inativo}
      accessibilityRole="button"
    >
      {carregando ? (
        <ActivityIndicator size="small" color={ehPrimario ? '#ffffff' : cores.marca} />
      ) : null}
      <Text style={ehPrimario ? estilos.botaoPrimarioTexto : estilos.botaoSecundarioTexto}>
        {titulo}
      </Text>
    </TouchableOpacity>
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
  campo: {
    marginBottom: espacos.md,
  },
  rotulo: {
    fontSize: 13,
    fontWeight: '600',
    color: cores.textoSecundario,
    marginBottom: 6,
  },
  obrigatorio: {
    color: cores.perigo,
  },
  entrada: {
    backgroundColor: cores.superficie,
    borderWidth: 1,
    borderColor: cores.borda,
    borderRadius: raios.md,
    paddingHorizontal: espacos.md,
    paddingVertical: 11,
    fontSize: 15,
    color: cores.texto,
  },
  dica: {
    marginTop: 4,
    fontSize: 12,
    color: cores.textoSuave,
  },
  opcoes: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: espacos.sm,
  },
  opcao: {
    paddingHorizontal: espacos.md,
    paddingVertical: 9,
    borderRadius: raios.cheio,
    borderWidth: 1,
    borderColor: cores.borda,
    backgroundColor: cores.superficie,
  },
  opcaoAtiva: {
    borderColor: cores.marca,
    backgroundColor: cores.marcaClara,
  },
  opcaoTexto: {
    fontSize: 14,
    color: cores.textoSecundario,
  },
  opcaoTextoAtivo: {
    color: cores.marcaEscura,
    fontWeight: '700',
  },
  marcacao: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.sm,
    marginBottom: espacos.md,
  },
  caixa: {
    width: 22,
    height: 22,
    borderRadius: raios.sm,
    borderWidth: 1,
    borderColor: cores.borda,
    backgroundColor: cores.superficie,
    alignItems: 'center',
    justifyContent: 'center',
  },
  caixaMarcada: {
    backgroundColor: cores.marca,
    borderColor: cores.marca,
  },
  caixaTexto: {
    color: '#ffffff',
    fontSize: 13,
    fontWeight: '800',
  },
  marcacaoTexto: {
    flex: 1,
    fontSize: 14,
    color: cores.texto,
  },
  botao: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: espacos.sm,
    paddingVertical: 13,
    paddingHorizontal: espacos.md,
    borderRadius: raios.md,
  },
  botaoPrimario: {
    backgroundColor: cores.marca,
  },
  botaoPrimarioTexto: {
    color: '#ffffff',
    fontSize: 15,
    fontWeight: '700',
  },
  botaoSecundario: {
    backgroundColor: cores.superficie,
    borderWidth: 1,
    borderColor: cores.borda,
  },
  botaoSecundarioTexto: {
    color: cores.textoSecundario,
    fontSize: 15,
    fontWeight: '700',
  },
  botaoInativo: {
    opacity: 0.5,
  },
});
