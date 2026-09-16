import { useCallback, useState } from 'react';
import {
  Alert,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useFocusEffect } from '@react-navigation/native';
import { api, mensagemDeErro } from '../services/api';
import type { Sessao } from '../tipos';
import { Aviso, Cartao, Carregando, Etiqueta, SemDados } from '../componentes/ui';
import { Icone } from '../componentes/Icone';
import { cores, espacos, estiloStatus, raios } from '../tema';
import { formatarDataExtensa, formatarHora } from '../utils/formato';

/** HU-006 e HU-013, CA-3: agenda dos pets do tutor, com confirmação e cancelamento. */
export function Agenda() {
  const [sessoes, setSessoes] = useState<Sessao[]>([]);
  const [apenasFuturas, setApenasFuturas] = useState(true);
  const [carregando, setCarregando] = useState(true);
  const [atualizando, setAtualizando] = useState(false);
  const [processando, setProcessando] = useState<string | null>(null);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');

  const carregar = useCallback(async () => {
    setErro('');

    try {
      const { data } = await api.get<Sessao[]>('/api/sessoes/minhas', { params: { apenasFuturas } });
      setSessoes(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível carregar sua agenda.'));
    } finally {
      setCarregando(false);
      setAtualizando(false);
    }
  }, [apenasFuturas]);

  useFocusEffect(
    useCallback(() => {
      carregar();
    }, [carregar]),
  );

  /** HU-006, CA-1: confirmação de presença. */
  async function confirmar(sessao: Sessao) {
    await alterarStatus(sessao, 'Confirmada');
  }

  /** HU-006, CA-2: cancelamento, com confirmação explícita antes de agir. */
  function pedirCancelamento(sessao: Sessao) {
    Alert.alert(
      'Cancelar presença',
      `Deseja cancelar a sessão de ${sessao.nomePaciente} em ${formatarDataExtensa(sessao.dataHora)} às ${formatarHora(
        sessao.dataHora,
      )}?`,
      [
        { text: 'Manter sessão', style: 'cancel' },
        { text: 'Cancelar presença', style: 'destructive', onPress: () => alterarStatus(sessao, 'Cancelada') },
      ],
    );
  }

  async function alterarStatus(sessao: Sessao, status: 'Confirmada' | 'Cancelada') {
    setErro('');
    setAviso('');
    setProcessando(sessao.id);

    try {
      await api.patch(`/api/sessoes/${sessao.id}/status`, { status });

      setAviso(
        status === 'Confirmada'
          ? `Presença confirmada para a sessão de ${sessao.nomePaciente}.`
          : `Sessão de ${sessao.nomePaciente} cancelada. A clínica foi avisada.`,
      );

      await carregar();
    } catch (falha) {
      // RN-009: cancelamento fora do prazo mínimo devolve a explicação da API.
      setErro(mensagemDeErro(falha, 'Não foi possível atualizar a sessão.'));
    } finally {
      setProcessando(null);
    }
  }

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
        <Text style={estilos.titulo}>Minha agenda</Text>
        <Text style={estilos.subtitulo}>Confirme ou cancele a presença do seu pet.</Text>

        <Cartao estilo={estilos.filtro}>
          <Text style={estilos.filtroTexto}>Mostrar apenas as próximas sessões</Text>
          <Switch
            value={apenasFuturas}
            onValueChange={(valor) => {
              setApenasFuturas(valor);
              setCarregando(true);
            }}
            trackColor={{ false: cores.borda, true: cores.marcaClara }}
            thumbColor={apenasFuturas ? cores.marca : '#f1f5f9'}
          />
        </Cartao>

        {erro ? (
          <View style={estilos.mensagem}>
            <Aviso tipo="erro">{erro}</Aviso>
          </View>
        ) : null}

        {aviso ? (
          <View style={estilos.mensagem}>
            <Aviso tipo="sucesso">{aviso}</Aviso>
          </View>
        ) : null}

        {carregando ? (
          <Carregando texto="Carregando sua agenda..." />
        ) : sessoes.length === 0 ? (
          <Cartao>
            <SemDados
              icone="📅"
              titulo="Nenhuma sessão agendada"
              descricao="Assim que a clínica marcar uma sessão para o seu pet, ela aparece aqui."
            />
          </Cartao>
        ) : (
          sessoes.map((sessao) => {
            const estilo = estiloStatus[sessao.status] ?? { fundo: cores.fundo, texto: cores.textoSecundario };
            const emAndamento = processando === sessao.id;
            const encerrada = sessao.status === 'Cancelada' || sessao.status === 'Concluída';

            return (
              <Cartao key={sessao.id} estilo={estilos.cartaoSessao}>
                <View style={estilos.linhaTopo}>
                  <Text style={estilos.nomePaciente}>{sessao.nomePaciente}</Text>
                  <Etiqueta texto={sessao.status} fundo={estilo.fundo} cor={estilo.texto} />
                </View>

                <Text style={estilos.dataSessao}>
                  {formatarDataExtensa(sessao.dataHora)} às {formatarHora(sessao.dataHora)}
                </Text>

                {sessao.nomeVeterinario ? (
                  <Text style={estilos.veterinario}>Com {sessao.nomeVeterinario}</Text>
                ) : null}

                {sessao.observacoes ? (
                  <View style={estilos.observacao}>
                    <Icone nome="info" tamanho={14} cor={cores.marcaEscura} />
                    <Text style={estilos.observacaoTexto}>{sessao.observacoes}</Text>
                  </View>
                ) : null}

                {!encerrada && (
                  <View style={estilos.acoes}>
                    {sessao.status === 'Aguardando confirmação' && (
                      <TouchableOpacity
                        style={[estilos.botao, estilos.botaoConfirmar, emAndamento && estilos.botaoDesativado]}
                        onPress={() => confirmar(sessao)}
                        disabled={emAndamento}
                        accessibilityRole="button"
                      >
                        <Icone nome="ok" tamanho={15} cor="#ffffff" />
                        <Text style={estilos.botaoConfirmarTexto}>Confirmar</Text>
                      </TouchableOpacity>
                    )}

                    <TouchableOpacity
                      style={[
                        estilos.botao,
                        estilos.botaoCancelar,
                        (!sessao.podeCancelar || emAndamento) && estilos.botaoDesativado,
                      ]}
                      onPress={() => pedirCancelamento(sessao)}
                      disabled={!sessao.podeCancelar || emAndamento}
                      accessibilityRole="button"
                    >
                      <Icone nome="cancelar" tamanho={13} cor={cores.perigo} />
                      <Text style={estilos.botaoCancelarTexto}>Cancelar</Text>
                    </TouchableOpacity>
                  </View>
                )}

                {/* RN-009: avisamos antes de o tutor tentar uma ação que seria recusada. */}
                {!encerrada && !sessao.podeCancelar && (
                  <Text style={estilos.nota}>
                    O prazo para cancelar esta sessão pelo aplicativo já passou. Fale com a clínica
                    se precisar remarcar.
                  </Text>
                )}
              </Cartao>
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
    marginBottom: espacos.md,
  },
  filtro: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: espacos.sm,
    marginBottom: espacos.md,
  },
  filtroTexto: {
    flex: 1,
    fontSize: 14,
    color: cores.textoSecundario,
  },
  mensagem: {
    marginBottom: espacos.md,
  },
  cartaoSessao: {
    marginBottom: espacos.md,
    gap: 4,
  },
  linhaTopo: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: espacos.sm,
    marginBottom: 2,
  },
  nomePaciente: {
    flex: 1,
    fontSize: 18,
    fontWeight: '700',
    color: cores.texto,
  },
  dataSessao: {
    fontSize: 14,
    color: cores.textoSecundario,
    textTransform: 'capitalize',
  },
  veterinario: {
    fontSize: 13,
    color: cores.textoSuave,
  },
  observacao: {
    flexDirection: 'row',
    gap: espacos.sm,
    backgroundColor: cores.marcaClara,
    borderRadius: raios.md,
    padding: espacos.sm + 2,
    marginTop: espacos.sm,
  },
  observacaoTexto: {
    flex: 1,
    fontSize: 13,
    color: cores.marcaEscura,
    lineHeight: 18,
  },
  acoes: {
    flexDirection: 'row',
    gap: espacos.sm,
    marginTop: espacos.md,
  },
  botao: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 11,
    borderRadius: raios.md,
  },
  botaoConfirmar: {
    backgroundColor: cores.sucesso,
  },
  botaoConfirmarTexto: {
    color: '#ffffff',
    fontSize: 14,
    fontWeight: '700',
  },
  botaoCancelar: {
    backgroundColor: cores.superficie,
    borderWidth: 1,
    borderColor: cores.borda,
  },
  botaoCancelarTexto: {
    color: cores.perigo,
    fontSize: 14,
    fontWeight: '700',
  },
  botaoDesativado: {
    opacity: 0.45,
  },
  nota: {
    marginTop: espacos.sm,
    fontSize: 12,
    color: cores.textoSuave,
    lineHeight: 17,
  },
});
