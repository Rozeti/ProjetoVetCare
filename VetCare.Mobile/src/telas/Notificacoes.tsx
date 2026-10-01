import { useCallback, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useFocusEffect } from '@react-navigation/native';
import { api, mensagemDeErro } from '../services/api';
import { useAtualizacao } from '../contextos/AtualizacoesContext';
import { abrirDestinoDaNotificacao } from '../navegacao/rotas';
import type { Notificacao, TipoNotificacao } from '../tipos';
import { Alerta, Cartao, Carregando, SemDados } from '../componentes/ui';
import { cores, espacos, raios } from '../tema';
import { tempoRelativo } from '../utils/formato';

/** Ícone e cor de cada gatilho previsto na HU-015 (mesma leitura do portal web). */
const ESTILO_POR_TIPO: Record<TipoNotificacao, { icone: string; fundo: string }> = {
  SessaoAgendada: { icone: '📅', fundo: cores.marcaClara },
  LembreteConfirmacao: { icone: '⏰', fundo: cores.alertaClaro },
  StatusSessao: { icone: '🔄', fundo: cores.infoClaro },
  NovoRegistroProntuario: { icone: '🩺', fundo: cores.sucessoClaro },
  NovaMensagem: { icone: '💬', fundo: cores.marcaClara },
  DoseDeVacina: { icone: '💉', fundo: cores.alertaClaro },
};

/** HU-015: notificações dos eventos relevantes do tratamento. */
export function Notificacoes() {
  const [notificacoes, setNotificacoes] = useState<Notificacao[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [atualizando, setAtualizando] = useState(false);
  const [erro, setErro] = useState('');

  const carregar = useCallback(async () => {
    setErro('');

    try {
      const { data } = await api.get<Notificacao[]>('/api/notificacoes');
      setNotificacoes(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível carregar os avisos.'));
    } finally {
      setCarregando(false);
      setAtualizando(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      carregar();
    }, [carregar]),
  );

  useAtualizacao(['notificacoes'], carregar);

  async function marcarTodas() {
    try {
      await api.patch('/api/notificacoes/todas/visualizadas');
      await carregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível marcar os avisos.'));
    }
  }

  /** Marca como lido e leva à tela a que o aviso se refere (agenda, mensagens, prontuário). */
  async function abrir(notificacao: Notificacao) {
    if (!notificacao.visualizada) {
      setNotificacoes((atual) =>
        atual.map((n) => (n.id === notificacao.id ? { ...n, visualizada: true } : n)),
      );

      try {
        await api.patch(`/api/notificacoes/${notificacao.id}/visualizada`);
      } catch {
        // Falhar ao marcar não deve atrapalhar a leitura.
      }
    }

    if (notificacao.linkRelacionado) {
      abrirDestinoDaNotificacao(notificacao.linkRelacionado);
    }
  }

  const naoVisualizadas = notificacoes.filter((n) => !n.visualizada).length;

  return (
    <SafeAreaView style={estilos.area} edges={['top']}>
      <ScrollView
        contentContainerStyle={estilos.conteudo}
        refreshControl={
          <RefreshControl
            refreshing={atualizando}
            onRefresh={() => {
              setAtualizando(true);
              carregar();
            }}
            colors={[cores.marca]}
            tintColor={cores.marca}
          />
        }
      >
        <View style={estilos.cabecalho}>
          <View style={estilos.cabecalhoTexto}>
            <Text style={estilos.titulo}>Avisos</Text>
            <Text style={estilos.subtitulo}>
              {naoVisualizadas > 0
                ? `${naoVisualizadas} não ${naoVisualizadas === 1 ? 'lido' : 'lidos'}`
                : 'Tudo em dia'}
            </Text>
          </View>

          {naoVisualizadas > 0 && (
            <TouchableOpacity onPress={marcarTodas} accessibilityRole="button">
              <Text style={estilos.marcarTodas}>Marcar todos</Text>
            </TouchableOpacity>
          )}
        </View>

        {erro ? (
          <View style={estilos.espaco}>
            <Alerta tipo="erro">{erro}</Alerta>
          </View>
        ) : null}

        {carregando ? (
          <Carregando texto="Carregando avisos..." />
        ) : notificacoes.length === 0 ? (
          <Cartao>
            <SemDados
              icone="🔔"
              titulo="Nenhum aviso"
              descricao="Sessões agendadas, lembretes de confirmação, novos registros no prontuário, doses de vacina e mensagens aparecem aqui."
            />
          </Cartao>
        ) : (
          notificacoes.map((notificacao) => {
            const estilo = ESTILO_POR_TIPO[notificacao.tipo] ?? { icone: '🔔', fundo: cores.fundo };

            return (
              <TouchableOpacity
                key={notificacao.id}
                activeOpacity={0.7}
                onPress={() => abrir(notificacao)}
                accessibilityRole="button"
                accessibilityLabel={`${notificacao.visualizada ? '' : 'Novo: '}${notificacao.titulo}`}
                accessibilityHint={notificacao.linkRelacionado ? 'Abre a tela relacionada' : undefined}
              >
                <Cartao estilo={[estilos.cartao, !notificacao.visualizada && estilos.cartaoNovo]}>
                  <View style={[estilos.icone, { backgroundColor: estilo.fundo }]}>
                    <Text style={estilos.iconeTexto}>{estilo.icone}</Text>
                  </View>

                  <View style={estilos.info}>
                    <View style={estilos.linhaTitulo}>
                      <Text style={estilos.tituloNotificacao} numberOfLines={1}>
                        {notificacao.titulo}
                      </Text>
                      <Text style={estilos.tempo}>{tempoRelativo(notificacao.dataCriacao)}</Text>
                    </View>

                    <Text style={estilos.conteudoNotificacao}>{notificacao.conteudo}</Text>
                  </View>

                  {!notificacao.visualizada && <View style={estilos.pontoNovo} />}
                </Cartao>
              </TouchableOpacity>
            );
          })
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
  conteudo: {
    padding: espacos.md,
    paddingBottom: espacos.xl,
  },
  cabecalho: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: espacos.md,
    marginTop: espacos.sm,
    marginBottom: espacos.md,
  },
  cabecalhoTexto: {
    flex: 1,
  },
  titulo: {
    fontSize: 24,
    fontWeight: '800',
    color: cores.texto,
  },
  subtitulo: {
    fontSize: 14,
    color: cores.textoSecundario,
    marginTop: 2,
  },
  marcarTodas: {
    fontSize: 13,
    fontWeight: '700',
    color: cores.marca,
  },
  espaco: {
    marginBottom: espacos.md,
  },
  cartao: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: espacos.md,
    marginBottom: espacos.sm,
  },
  cartaoNovo: {
    borderColor: cores.marca,
  },
  icone: {
    width: 42,
    height: 42,
    borderRadius: raios.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  iconeTexto: {
    fontSize: 19,
  },
  info: {
    flex: 1,
    gap: 3,
  },
  linhaTitulo: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    gap: espacos.sm,
  },
  tituloNotificacao: {
    flex: 1,
    fontSize: 15,
    fontWeight: '700',
    color: cores.texto,
  },
  tempo: {
    fontSize: 11,
    color: cores.textoSuave,
  },
  conteudoNotificacao: {
    fontSize: 13,
    color: cores.textoSecundario,
    lineHeight: 19,
  },
  pontoNovo: {
    width: 9,
    height: 9,
    borderRadius: 5,
    backgroundColor: cores.marca,
    marginTop: 6,
  },
});
