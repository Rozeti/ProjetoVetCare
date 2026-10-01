import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { AuthProvider, useAuth } from './src/contextos/AuthContext';
import { AtualizacoesProvider } from './src/contextos/AtualizacoesContext';
import { Navegacao } from './src/Navegacao';
import { navegacaoRef } from './src/navegacao/rotas';
import { cores } from './src/tema';

/** Segura a interface enquanto a sessão guardada no aparelho é restaurada. */
function Raiz() {
  const { carregando } = useAuth();

  if (carregando) {
    return (
      <View style={estilos.carregando}>
        <ActivityIndicator size="large" color={cores.marca} />
      </View>
    );
  }

  return <Navegacao />;
}

export default function App() {
  return (
    <SafeAreaProvider>
      <AuthProvider>
        {/* Uma única conexão com o mural de alterações serve todas as telas. */}
        <AtualizacoesProvider>
          {/* A referência permite abrir a tela certa ao tocar numa notificação. */}
          <NavigationContainer ref={navegacaoRef}>
            <StatusBar style="dark" />
            <Raiz />
          </NavigationContainer>
        </AtualizacoesProvider>
      </AuthProvider>
    </SafeAreaProvider>
  );
}

const estilos = StyleSheet.create({
  carregando: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: cores.fundo,
  },
});
