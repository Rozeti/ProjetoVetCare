import { useCallback, useState } from 'react';
import {
  FlatList,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useFocusEffect } from '@react-navigation/native';
import { api, mensagemDeErro } from '../services/api';
import type { Conversa, Mensagem, Usuario } from '../tipos';
import { Avatar, Aviso, Cartao, Carregando, SemDados } from '../componentes/ui';
import { Icone } from '../componentes/Icone';
import { cores, espacos, raios } from '../tema';
import { formatarHora, tempoRelativo } from '../utils/formato';

/** HU-014: troca de mensagens entre o tutor e a equipe clínica. */
export function Mensagens() {
  const [conversas, setConversas] = useState<Conversa[]>([]);
  const [contatos, setContatos] = useState<Usuario[]>([]);
  const [selecionado, setSelecionado] = useState<{ id: string; nome: string } | null>(null);
  const [mensagens, setMensagens] = useState<Mensagem[]>([]);
  const [texto, setTexto] = useState('');

  const [carregando, setCarregando] = useState(true);
  const [carregandoConversa, setCarregandoConversa] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState('');

  const carregarConversas = useCallback(async () => {
    setErro('');

    try {
      const [respostaConversas, respostaContatos] = await Promise.all([
        api.get<Conversa[]>('/api/mensagens/conversas'),
        api.get<Usuario[]>('/api/mensagens/contatos'),
      ]);

      setConversas(respostaConversas.data);
      setContatos(respostaContatos.data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível carregar as conversas.'));
    } finally {
      setCarregando(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      if (!selecionado) carregarConversas();
    }, [carregarConversas, selecionado]),
  );

  /** HU-014, CA-3: abrir a conversa marca as mensagens recebidas como lidas. */
  const abrirConversa = useCallback(async (usuarioId: string, nome: string) => {
    setSelecionado({ id: usuarioId, nome });
    setCarregandoConversa(true);
    setErro('');

    try {
      const { data } = await api.get<Mensagem[]>(`/api/mensagens/conversa/${usuarioId}`);
      setMensagens(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível abrir a conversa.'));
    } finally {
      setCarregandoConversa(false);
    }
  }, []);

  async function enviar() {
    if (!selecionado || !texto.trim()) return;

    setEnviando(true);
    setErro('');

    try {
      const { data } = await api.post<Mensagem>('/api/mensagens', {
        destinatarioId: selecionado.id,
        conteudo: texto.trim(),
      });

      setMensagens((atual) => [...atual, data]);
      setTexto('');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível enviar a mensagem.'));
    } finally {
      setEnviando(false);
    }
  }

  function voltar() {
    setSelecionado(null);
    setMensagens([]);
    carregarConversas();
  }

  // ---- Conversa aberta
  if (selecionado) {
    return (
      <SafeAreaView style={estilos.area} edges={['top']}>
        <View style={estilos.cabecalhoConversa}>
          <TouchableOpacity onPress={voltar} style={estilos.voltar} accessibilityRole="button" accessibilityLabel="Voltar">
            <Icone nome="voltar" tamanho={28} cor={cores.marca} />
          </TouchableOpacity>

          <Avatar nome={selecionado.nome} tamanho={36} />
          <Text style={estilos.tituloConversa} numberOfLines={1}>
            {selecionado.nome}
          </Text>
        </View>

        <KeyboardAvoidingView
          style={estilos.flex}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          keyboardVerticalOffset={Platform.OS === 'ios' ? 90 : 0}
        >
          {carregandoConversa ? (
            <Carregando texto="Carregando mensagens..." />
          ) : (
            <FlatList
              data={mensagens}
              keyExtractor={(item) => item.id}
              contentContainerStyle={estilos.listaMensagens}
              ListEmptyComponent={
                <SemDados icone="💬" titulo="Nenhuma mensagem ainda" descricao="Escreva a primeira mensagem abaixo." />
              }
              renderItem={({ item }) => (
                <View style={[estilos.balaoLinha, item.propria ? estilos.balaoDireita : estilos.balaoEsquerda]}>
                  <View style={[estilos.balao, item.propria ? estilos.balaoProprio : estilos.balaoOutro]}>
                    {item.nomePaciente ? (
                      <Text style={[estilos.balaoAssunto, item.propria && estilos.balaoAssuntoProprio]}>
                        Sobre {item.nomePaciente}
                      </Text>
                    ) : null}

                    <Text style={[estilos.balaoTexto, item.propria && estilos.balaoTextoProprio]}>
                      {item.conteudo}
                    </Text>

                    <Text style={[estilos.balaoHora, item.propria && estilos.balaoHoraPropria]}>
                      {formatarHora(item.dataEnvio)}
                    </Text>
                  </View>
                </View>
              )}
            />
          )}

          {erro ? (
            <View style={estilos.erroConversa}>
              <Aviso tipo="erro">{erro}</Aviso>
            </View>
          ) : null}

          <View style={estilos.barraEnvio}>
            <TextInput
              style={estilos.campoMensagem}
              placeholder="Escreva sua mensagem..."
              placeholderTextColor={cores.textoSuave}
              value={texto}
              onChangeText={setTexto}
              multiline
              editable={!enviando}
            />

            <TouchableOpacity
              style={[estilos.botaoEnviar, (!texto.trim() || enviando) && estilos.botaoDesativado]}
              onPress={enviar}
              disabled={!texto.trim() || enviando}
              accessibilityRole="button"
              accessibilityLabel="Enviar mensagem"
            >
              <Icone nome="enviar" tamanho={18} cor="#ffffff" />
            </TouchableOpacity>
          </View>
        </KeyboardAvoidingView>
      </SafeAreaView>
    );
  }

  // ---- Lista de conversas
  return (
    <SafeAreaView style={estilos.area} edges={['top']}>
      <ScrollView contentContainerStyle={estilos.conteudo}>
        <Text style={estilos.titulo}>Mensagens</Text>
        <Text style={estilos.subtitulo}>Fale com a equipe da clínica sobre o tratamento.</Text>

        {erro ? (
          <View style={estilos.espaco}>
            <Aviso tipo="erro">{erro}</Aviso>
          </View>
        ) : null}

        {carregando ? (
          <Carregando texto="Carregando conversas..." />
        ) : (
          <>
            {conversas.length > 0 && (
              <>
                <Text style={estilos.secao}>Suas conversas</Text>

                {conversas.map((conversa) => (
                  <TouchableOpacity
                    key={conversa.usuarioId}
                    activeOpacity={0.7}
                    onPress={() => abrirConversa(conversa.usuarioId, conversa.nome)}
                    accessibilityRole="button"
                  >
                    <Cartao estilo={estilos.cartaoConversa}>
                      <View>
                        <Avatar nome={conversa.nome} tamanho={44} />
                        {/* HU-014, CA-2: indicador de presença. */}
                        {conversa.online && <View style={estilos.pontoOnline} />}
                      </View>

                      <View style={estilos.infoConversa}>
                        <View style={estilos.linhaConversa}>
                          <Text style={estilos.nomeConversa} numberOfLines={1}>
                            {conversa.nome}
                          </Text>
                          <Text style={estilos.horaConversa}>{tempoRelativo(conversa.dataUltimaMensagem)}</Text>
                        </View>

                        <Text style={estilos.previa} numberOfLines={1}>
                          {conversa.ultimaMensagem}
                        </Text>
                      </View>

                      {conversa.naoLidas > 0 && (
                        <View style={estilos.contador}>
                          <Text style={estilos.contadorTexto}>{conversa.naoLidas}</Text>
                        </View>
                      )}
                    </Cartao>
                  </TouchableOpacity>
                ))}
              </>
            )}

            <Text style={estilos.secao}>Equipe da clínica</Text>

            {contatos.length === 0 ? (
              <Cartao>
                <SemDados icone="💬" titulo="Nenhum contato disponível" />
              </Cartao>
            ) : (
              contatos.map((contato) => (
                <TouchableOpacity
                  key={contato.id}
                  activeOpacity={0.7}
                  onPress={() => abrirConversa(contato.id, contato.nome)}
                  accessibilityRole="button"
                >
                  <Cartao estilo={estilos.cartaoConversa}>
                    <Avatar nome={contato.nome} tamanho={44} />

                    <View style={estilos.infoConversa}>
                      <Text style={estilos.nomeConversa}>{contato.nome}</Text>
                      <Text style={estilos.previa}>{contato.perfil}</Text>
                    </View>

                    <Icone nome="seta" tamanho={24} cor={cores.textoSuave} />
                  </Cartao>
                </TouchableOpacity>
              ))
            )}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

const estilos = StyleSheet.create({
  area: {
    flex: 1,
    backgroundColor: cores.fundo,
  },
  flex: {
    flex: 1,
  },
  conteudo: {
    padding: espacos.md,
    paddingBottom: espacos.xl,
  },
  titulo: {
    fontSize: 24,
    fontWeight: '800',
    color: cores.texto,
    marginTop: espacos.sm,
  },
  subtitulo: {
    fontSize: 14,
    color: cores.textoSecundario,
    marginTop: 2,
  },
  secao: {
    fontSize: 13,
    fontWeight: '700',
    color: cores.textoSecundario,
    textTransform: 'uppercase',
    marginTop: espacos.lg,
    marginBottom: espacos.sm,
  },
  espaco: {
    marginTop: espacos.md,
  },
  cartaoConversa: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.md,
    marginBottom: espacos.sm,
  },
  pontoOnline: {
    position: 'absolute',
    right: -1,
    bottom: -1,
    width: 13,
    height: 13,
    borderRadius: 7,
    backgroundColor: cores.sucesso,
    borderWidth: 2,
    borderColor: cores.superficie,
  },
  infoConversa: {
    flex: 1,
    gap: 2,
  },
  linhaConversa: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    gap: espacos.sm,
  },
  nomeConversa: {
    flex: 1,
    fontSize: 15,
    fontWeight: '700',
    color: cores.texto,
  },
  horaConversa: {
    fontSize: 11,
    color: cores.textoSuave,
  },
  previa: {
    fontSize: 13,
    color: cores.textoSecundario,
  },
  contador: {
    minWidth: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: cores.marca,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 6,
  },
  contadorTexto: {
    color: '#ffffff',
    fontSize: 11,
    fontWeight: '700',
  },
  cabecalhoConversa: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.sm,
    paddingHorizontal: espacos.sm,
    paddingVertical: espacos.sm,
    backgroundColor: cores.superficie,
    borderBottomWidth: 1,
    borderBottomColor: cores.borda,
  },
  voltar: {
    padding: espacos.sm,
  },
  tituloConversa: {
    flex: 1,
    fontSize: 17,
    fontWeight: '700',
    color: cores.texto,
  },
  listaMensagens: {
    padding: espacos.md,
    gap: espacos.sm,
    flexGrow: 1,
  },
  balaoLinha: {
    flexDirection: 'row',
  },
  balaoEsquerda: {
    justifyContent: 'flex-start',
  },
  balaoDireita: {
    justifyContent: 'flex-end',
  },
  balao: {
    maxWidth: '80%',
    borderRadius: raios.lg,
    paddingHorizontal: espacos.md,
    paddingVertical: espacos.sm + 2,
  },
  balaoProprio: {
    backgroundColor: cores.marca,
    borderBottomRightRadius: 4,
  },
  balaoOutro: {
    backgroundColor: cores.superficie,
    borderBottomLeftRadius: 4,
    borderWidth: 1,
    borderColor: cores.borda,
  },
  balaoAssunto: {
    fontSize: 11,
    fontWeight: '700',
    color: cores.textoSecundario,
    marginBottom: 3,
  },
  balaoAssuntoProprio: {
    color: cores.marcaClara,
  },
  balaoTexto: {
    fontSize: 14,
    lineHeight: 20,
    color: cores.texto,
  },
  balaoTextoProprio: {
    color: '#ffffff',
  },
  balaoHora: {
    fontSize: 10,
    color: cores.textoSuave,
    textAlign: 'right',
    marginTop: 4,
  },
  balaoHoraPropria: {
    color: cores.marcaClara,
  },
  erroConversa: {
    paddingHorizontal: espacos.md,
    paddingBottom: espacos.sm,
  },
  barraEnvio: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: espacos.sm,
    padding: espacos.md,
    backgroundColor: cores.superficie,
    borderTopWidth: 1,
    borderTopColor: cores.borda,
  },
  campoMensagem: {
    flex: 1,
    maxHeight: 110,
    borderWidth: 1,
    borderColor: cores.borda,
    borderRadius: raios.md,
    paddingHorizontal: espacos.md,
    paddingVertical: espacos.sm + 2,
    fontSize: 14,
    color: cores.texto,
    backgroundColor: cores.fundo,
  },
  botaoEnviar: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: cores.marca,
    alignItems: 'center',
    justifyContent: 'center',
  },
  botaoDesativado: {
    opacity: 0.45,
  },
});
