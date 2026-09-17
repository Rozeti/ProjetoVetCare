import { useCallback, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useFocusEffect, type NavigationProp, useNavigation } from '@react-navigation/native';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contextos/AuthContext';
import { useAtualizacao } from '../contextos/AtualizacoesContext';
import type { Pet, Sessao } from '../tipos';
import { Avatar, Aviso, Botao, Cartao, Carregando, Etiqueta, SemDados, TituloSecao } from '../componentes/ui';
import { FormularioNovoPet } from '../componentes/FormularioNovoPet';
import { Icone, iconeDaEspecie } from '../componentes/Icone';
import { cores, espacos, estiloStatus, raios } from '../tema';
import { diaDoMes, formatarHora, formatarPeso, mesAbreviado } from '../utils/formato';

/** HU-013, CA-1: o tutor vê apenas os pets sob sua responsabilidade. */
export function MeusPets() {
  const navegacao = useNavigation<NavigationProp<Record<string, object | undefined>>>();
  const { usuario } = useAuth();

  const [pets, setPets] = useState<Pet[]>([]);
  const [sessoes, setSessoes] = useState<Sessao[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [atualizando, setAtualizando] = useState(false);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [cadastroAberto, setCadastroAberto] = useState(false);

  const carregar = useCallback(async () => {
    setErro('');

    try {
      const [respostaPets, respostaSessoes] = await Promise.all([
        api.get<Pet[]>('/api/pets/meus'),
        api.get<Sessao[]>('/api/sessoes/minhas', { params: { apenasFuturas: true } }),
      ]);

      setPets(respostaPets.data);
      setSessoes(respostaSessoes.data.slice(0, 3));
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível carregar seus dados.'));
    } finally {
      setCarregando(false);
      setAtualizando(false);
    }
  }, []);

  // Recarrega ao voltar para a aba: a agenda pode ter mudado em outra tela.
  useFocusEffect(
    useCallback(() => {
      carregar();
    }, [carregar]),
  );

  // E também no instante em que a clínica mexe em algo — sem esperar a próxima visita
  // à aba nem o tutor puxar a lista para baixo.
  useAtualizacao(['pets', 'sessoes', 'tratamentos', 'alergias', 'vacinas'], carregar);

  function aoCadastrarPet(pet: Pet) {
    setCadastroAberto(false);
    setAviso(`${pet.nome} foi cadastrado e já aparece para a equipe da clínica.`);
    carregar();
  }

  const primeiroNome = usuario?.nome.split(' ')[0] ?? '';

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
          <View style={estilos.saudacaoBloco}>
            <Text style={estilos.saudacao}>Olá, {primeiroNome}</Text>
            <Text style={estilos.subtitulo}>Acompanhe a saúde dos seus pets.</Text>
          </View>
          <Avatar nome={usuario?.nome ?? ''} tamanho={44} />
        </View>

        {erro ? (
          <View style={estilos.espacoInferior}>
            <Aviso tipo="erro">{erro}</Aviso>
          </View>
        ) : null}

        {aviso ? (
          <TouchableOpacity
            style={estilos.espacoInferior}
            onPress={() => setAviso('')}
            accessibilityRole="button"
            accessibilityLabel="Dispensar aviso"
          >
            <Aviso tipo="sucesso">{aviso}</Aviso>
          </TouchableOpacity>
        ) : null}

        {carregando ? (
          <Carregando texto="Carregando seus pets..." />
        ) : (
          <>
            <View style={estilos.linhaSecao}>
              <TituloSecao estilo={estilos.tituloSemMargem}>Meus pets</TituloSecao>

              {/* Adquiriu um animal novo? O cadastro é feito aqui, sem ir à clínica. */}
              <TouchableOpacity
                style={estilos.botaoCadastrar}
                onPress={() => setCadastroAberto(true)}
                accessibilityRole="button"
                accessibilityLabel="Cadastrar um novo pet"
              >
                <Text style={estilos.botaoCadastrarTexto}>+ Cadastrar</Text>
              </TouchableOpacity>
            </View>

            {pets.length === 0 ? (
              <Cartao>
                <SemDados
                  icone="🐾"
                  titulo="Nenhum pet cadastrado"
                  descricao="Cadastre aqui o animal que acabou de chegar à família. Pets cadastrados pela clínica também aparecem nesta lista."
                />
                <Botao
                  titulo="Cadastrar meu pet"
                  aoPressionar={() => setCadastroAberto(true)}
                  estilo={estilos.botaoVazio}
                />
              </Cartao>
            ) : (
              pets.map((pet) => (
                <TouchableOpacity
                  key={pet.id}
                  activeOpacity={0.7}
                  onPress={() => navegacao.navigate('Prontuario', { pacienteId: pet.id, nome: pet.nome })}
                  accessibilityRole="button"
                  accessibilityLabel={`Abrir prontuário de ${pet.nome}`}
                >
                  <Cartao estilo={estilos.cartaoPet}>
                    <View style={estilos.iconePet}>
                      <Icone nome={iconeDaEspecie(pet.especie)} tamanho={28} />
                    </View>

                    <View style={estilos.infoPet}>
                      <Text style={estilos.nomePet}>{pet.nome}</Text>
                      <Text style={estilos.detalhePet}>
                        {pet.especie}
                        {pet.raca ? ` · ${pet.raca}` : ''}
                      </Text>
                      <Text style={estilos.detalhePet}>
                        {pet.idadeAnos} {pet.idadeAnos === 1 ? 'ano' : 'anos'} · {formatarPeso(pet.pesoAtualKg)}
                      </Text>
                    </View>

                    <Icone nome="seta" tamanho={26} cor={cores.textoSuave} />
                  </Cartao>
                </TouchableOpacity>
              ))
            )}

            <TituloSecao>Próximas sessões</TituloSecao>

            {sessoes.length === 0 ? (
              <Cartao>
                <SemDados
                  icone="📅"
                  titulo="Nenhuma sessão agendada"
                  descricao="Assim que a clínica marcar uma sessão, você recebe uma notificação."
                />
              </Cartao>
            ) : (
              sessoes.map((sessao) => {
                const estilo = estiloStatus[sessao.status] ?? { fundo: cores.fundo, texto: cores.textoSecundario };

                return (
                  <TouchableOpacity
                    key={sessao.id}
                    activeOpacity={0.7}
                    onPress={() => navegacao.navigate('Agenda' as never)}
                    accessibilityRole="button"
                  >
                    <Cartao estilo={estilos.cartaoSessao}>
                      <View style={estilos.dataBloco}>
                        <Text style={estilos.dia}>{diaDoMes(sessao.dataHora)}</Text>
                        <Text style={estilos.mes}>{mesAbreviado(sessao.dataHora)}</Text>
                      </View>

                      <View style={estilos.infoSessao}>
                        <Text style={estilos.nomePet}>{sessao.nomePaciente}</Text>
                        <Text style={estilos.detalhePet}>
                          {formatarHora(sessao.dataHora)}
                          {sessao.nomeVeterinario ? ` · ${sessao.nomeVeterinario}` : ''}
                        </Text>

                        <View style={estilos.etiquetaLinha}>
                          <Etiqueta texto={sessao.status} fundo={estilo.fundo} cor={estilo.texto} />
                        </View>
                      </View>
                    </Cartao>
                  </TouchableOpacity>
                );
              })
            )}

            <View style={estilos.dica}>
              <Aviso tipo="info">
                Lembre-se de não alimentar o pet nas duas horas anteriores à sessão de fisioterapia.
              </Aviso>
            </View>
          </>
        )}
      </ScrollView>

      <FormularioNovoPet
        aberto={cadastroAberto}
        aoFechar={() => setCadastroAberto(false)}
        aoCadastrar={aoCadastrarPet}
      />
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
  },
  saudacaoBloco: {
    flex: 1,
  },
  saudacao: {
    fontSize: 24,
    fontWeight: '800',
    color: cores.texto,
  },
  subtitulo: {
    fontSize: 14,
    color: cores.textoSecundario,
    marginTop: 2,
  },
  cartaoPet: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.md,
    marginBottom: espacos.sm,
  },
  iconePet: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: cores.marcaClara,
    alignItems: 'center',
    justifyContent: 'center',
  },
  infoPet: {
    flex: 1,
    gap: 2,
  },
  nomePet: {
    fontSize: 17,
    fontWeight: '700',
    color: cores.texto,
  },
  detalhePet: {
    fontSize: 13,
    color: cores.textoSecundario,
  },
  cartaoSessao: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.md,
    marginBottom: espacos.sm,
  },
  dataBloco: {
    backgroundColor: cores.fundo,
    borderRadius: raios.md,
    paddingVertical: espacos.sm,
    paddingHorizontal: espacos.md,
    alignItems: 'center',
    minWidth: 60,
  },
  dia: {
    fontSize: 22,
    fontWeight: '800',
    color: cores.texto,
  },
  mes: {
    fontSize: 11,
    fontWeight: '700',
    color: cores.marca,
  },
  infoSessao: {
    flex: 1,
    gap: 2,
  },
  etiquetaLinha: {
    marginTop: 6,
  },
  dica: {
    marginTop: espacos.lg,
  },
  linhaSecao: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: espacos.sm,
    marginTop: espacos.lg,
    marginBottom: espacos.sm,
  },
  tituloSemMargem: {
    marginTop: 0,
    marginBottom: 0,
  },
  botaoCadastrar: {
    paddingHorizontal: espacos.md,
    paddingVertical: 7,
    borderRadius: raios.cheio,
    backgroundColor: cores.marcaClara,
  },
  botaoCadastrarTexto: {
    fontSize: 13,
    fontWeight: '700',
    color: cores.marcaEscura,
  },
  botaoVazio: {
    marginTop: espacos.sm,
  },
  espacoInferior: {
    marginTop: espacos.md,
  },
});
