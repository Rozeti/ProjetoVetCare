import { useCallback, useEffect, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import * as Notifications from 'expo-notifications';
import { api } from './services/api';
import { dadosDaResposta } from './services/notificacoesPush';
import { useAuth } from './contextos/AuthContext';
import { useAtualizacao } from './contextos/AtualizacoesContext';
import { abrirDestinoDaNotificacao, type RotasDaPilha, type RotasDasAbas } from './navegacao/rotas';
import { Icone } from './componentes/Icone';
import { cores } from './tema';

import { Login } from './telas/Login';
import { RecuperarSenha } from './telas/RecuperarSenha';
import { MeusPets } from './telas/MeusPets';
import { MinhaAgenda } from './telas/MinhaAgenda';
import { Prontuario } from './telas/Prontuario';
import { Mensagens } from './telas/Mensagens';
import { Notificacoes } from './telas/Notificacoes';
import { Perfil } from './telas/Perfil';

const Pilha = createNativeStackNavigator<RotasDaPilha>();
const Abas = createBottomTabNavigator<RotasDasAbas>();

/** Contador exibido sobre o ícone da aba (mensagens e notificações). */
function IconeDaAba({ nome, cor, contador }: { nome: string; cor: string; contador?: number }) {
  return (
    <View>
      <Icone nome={nome} tamanho={20} cor={cor} />
      {contador ? (
        <View style={estilos.badge}>
          <Text style={estilos.badgeTexto}>{contador > 9 ? '9+' : contador}</Text>
        </View>
      ) : null}
    </View>
  );
}

function AbasDoTutor() {
  const insets = useSafeAreaInsets();
  const [naoLidas, setNaoLidas] = useState(0);
  const [notificacoes, setNotificacoes] = useState(0);

  const atualizarContadores = useCallback(async () => {
    try {
      const [mensagens, avisos] = await Promise.all([
        api.get<number>('/api/mensagens/nao-lidas'),
        api.get<number>('/api/notificacoes/nao-visualizadas'),
      ]);

      setNaoLidas(mensagens.data);
      setNotificacoes(avisos.data);
    } catch {
      // Contadores são informativos: falhar aqui não pode travar a navegação.
    }
  }, []);

  // HU-015: os contadores acompanham as novidades sem exigir ação do usuário. O mural
  // de atualizações avisa na hora; o temporizador fica como rede de segurança para o
  // caso de a conexão com ele estar caída.
  useAtualizacao(['mensagens', 'notificacoes', 'sessoes'], atualizarContadores);

  useEffect(() => {
    atualizarContadores();

    const intervalo = setInterval(atualizarContadores, 60_000);
    return () => clearInterval(intervalo);
  }, [atualizarContadores]);

  return (
    <Abas.Navigator
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: cores.marca,
        tabBarInactiveTintColor: cores.textoSuave,
        // A barra respeita a área segura: em aparelhos com gestos ou "home indicator" os
        // rótulos não ficam escondidos atrás da barra do sistema.
        tabBarStyle: [estilos.barra, { height: 62 + insets.bottom, paddingBottom: 8 + insets.bottom }],
        tabBarLabelStyle: estilos.rotulo,
      }}
    >
      <Abas.Screen
        name="MeusPets"
        component={MeusPets}
        options={{
          title: 'Meus pets',
          tabBarIcon: ({ color }) => <IconeDaAba nome="pet" cor={color} />,
        }}
      />

      <Abas.Screen
        name="MinhaAgenda"
        component={MinhaAgenda}
        options={{
          title: 'Agenda',
          tabBarIcon: ({ color }) => <IconeDaAba nome="agenda" cor={color} />,
        }}
      />

      <Abas.Screen
        name="Mensagens"
        component={Mensagens}
        options={{
          title: 'Mensagens',
          tabBarIcon: ({ color }) => <IconeDaAba nome="mensagem" cor={color} contador={naoLidas} />,
        }}
        listeners={{ focus: () => atualizarContadores() }}
      />

      <Abas.Screen
        name="Notificacoes"
        component={Notificacoes}
        options={{
          title: 'Avisos',
          tabBarIcon: ({ color }) => <IconeDaAba nome="sino" cor={color} contador={notificacoes} />,
        }}
        listeners={{ focus: () => atualizarContadores() }}
      />

      <Abas.Screen
        name="Perfil"
        component={Perfil}
        options={{
          title: 'Perfil',
          tabBarIcon: ({ color }) => <IconeDaAba nome="perfil" cor={color} />,
        }}
      />
    </Abas.Navigator>
  );
}

/**
 * Abre a tela certa quando o tutor toca numa notificação — tanto com o aplicativo
 * aberto quanto quando é a notificação que o abre. Cada resposta é tratada uma vez.
 */
function useAberturaPorNotificacao(autenticado: boolean) {
  const ultimaResposta = Notifications.useLastNotificationResponse();
  const tratada = useRef<string | null>(null);

  useEffect(() => {
    if (!autenticado || !ultimaResposta) {
      return;
    }

    const identificador = ultimaResposta.notification.request.identifier;

    if (tratada.current === identificador) {
      return;
    }

    const { link } = dadosDaResposta(ultimaResposta);
    let tentativas = 0;

    // Numa abertura a frio o navegador pode ainda não estar pronto; insistimos por alguns segundos.
    const temporizador = setInterval(() => {
      tentativas += 1;

      if (abrirDestinoDaNotificacao(link) || tentativas > 20) {
        tratada.current = identificador;
        clearInterval(temporizador);
      }
    }, 150);

    return () => clearInterval(temporizador);
  }, [autenticado, ultimaResposta]);
}

/**
 * A pilha alterna entre Login e a área autenticada conforme o estado do
 * AuthContext — assim não é preciso navegar manualmente após entrar ou sair.
 */
export function Navegacao() {
  const { usuario } = useAuth();

  useAberturaPorNotificacao(!!usuario);

  return (
    <Pilha.Navigator screenOptions={{ headerShown: false }}>
      {usuario ? (
        <>
          <Pilha.Screen name="Principal" component={AbasDoTutor} />
          <Pilha.Screen name="Prontuario" component={Prontuario} />
        </>
      ) : (
        <>
          <Pilha.Screen name="Login" component={Login} />
          <Pilha.Screen name="RecuperarSenha" component={RecuperarSenha} />
        </>
      )}
    </Pilha.Navigator>
  );
}

const estilos = StyleSheet.create({
  barra: {
    backgroundColor: cores.superficie,
    borderTopColor: cores.borda,
    paddingTop: 6,
  },
  rotulo: {
    fontSize: 11,
    fontWeight: '600',
  },
  badge: {
    position: 'absolute',
    top: -5,
    right: -9,
    minWidth: 16,
    height: 16,
    borderRadius: 8,
    backgroundColor: cores.perigo,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 3,
  },
  badgeTexto: {
    color: '#ffffff',
    fontSize: 9,
    fontWeight: '700',
  },
});
