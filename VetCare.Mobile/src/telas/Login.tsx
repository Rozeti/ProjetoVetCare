import { useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useNavigation, useRoute, type NavigationProp, type RouteProp } from '@react-navigation/native';
import { useAuth } from '../contextos/AuthContext';
import { mensagemDeErro } from '../services/api';
import type { RotasDaPilha } from '../navegacao/rotas';
import { Alerta } from '../componentes/ui';
import { cores, espacos, raios, sombraCard } from '../tema';

/** HU-001: autenticação do tutor no aplicativo. */
export function Login() {
  const { entrar } = useAuth();
  const navegacao = useNavigation<NavigationProp<RotasDaPilha>>();
  const rota = useRoute<RouteProp<RotasDaPilha, 'Login'>>();

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState('');
  const [enviando, setEnviando] = useState(false);

  // Mensagem de uma tela anterior (por exemplo, "senha redefinida").
  const mensagemDeSucesso = rota.params?.mensagem;

  async function aoEntrar() {
    setErro('');

    if (!email.trim() || !senha) {
      setErro('Informe o e-mail e a senha para entrar.');
      return;
    }

    setEnviando(true);

    try {
      await entrar(email.trim(), senha);
      // A navegação acontece sozinha: o App troca de pilha quando há usuário.
    } catch (falha) {
      // HU-001, CA-2/CA-3/CA-5: a mensagem vem da API — genérica para credenciais
      // inválidas, específica para conta inativa ou bloqueio por tentativas.
      setErro(mensagemDeErro(falha, 'Não foi possível entrar. Tente novamente.'));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={estilos.container}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <ScrollView contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
        <View style={estilos.cabecalho}>
          <View style={estilos.logo}>
            <Text style={estilos.logoIcone}>🩺</Text>
          </View>
          <Text style={estilos.titulo}>VetCare</Text>
          <Text style={estilos.subtitulo}>Portal do Tutor</Text>
        </View>

        <View style={estilos.formulario}>
          {mensagemDeSucesso ? (
            <View style={estilos.mensagem}>
              <Alerta tipo="sucesso">{mensagemDeSucesso}</Alerta>
            </View>
          ) : null}

          <Text style={estilos.rotulo}>E-mail</Text>
          <TextInput
            style={estilos.campo}
            placeholder="seu@email.com"
            placeholderTextColor={cores.textoSuave}
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoComplete="email"
            editable={!enviando}
          />

          <Text style={estilos.rotulo}>Senha</Text>
          <TextInput
            style={estilos.campo}
            placeholder="••••••••"
            placeholderTextColor={cores.textoSuave}
            value={senha}
            onChangeText={setSenha}
            secureTextEntry
            autoComplete="current-password"
            editable={!enviando}
            onSubmitEditing={aoEntrar}
            returnKeyType="go"
          />

          {erro ? (
            <View style={estilos.mensagem}>
              <Alerta tipo="erro">{erro}</Alerta>
            </View>
          ) : null}

          <TouchableOpacity
            style={[estilos.botao, enviando && estilos.botaoDesativado]}
            onPress={aoEntrar}
            disabled={enviando}
            accessibilityRole="button"
          >
            {enviando ? (
              <ActivityIndicator color="#ffffff" />
            ) : (
              <Text style={estilos.botaoTexto}>Entrar no aplicativo</Text>
            )}
          </TouchableOpacity>

          <TouchableOpacity
            onPress={() => navegacao.navigate('RecuperarSenha')}
            disabled={enviando}
            accessibilityRole="button"
            accessibilityLabel="Esqueci minha senha"
          >
            <Text style={estilos.ligacao}>Esqueci minha senha</Text>
          </TouchableOpacity>

          <Text style={estilos.ajuda}>
            Primeiro acesso? Use o código recebido no e-mail de boas-vindas em "Esqueci minha senha" para
            criar a sua senha.
          </Text>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const estilos = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: cores.fundo,
  },
  conteudo: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: espacos.lg,
  },
  cabecalho: {
    alignItems: 'center',
    marginBottom: espacos.xl,
  },
  logo: {
    width: 72,
    height: 72,
    borderRadius: raios.lg,
    backgroundColor: cores.marca,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: espacos.md,
    ...sombraCard,
  },
  logoIcone: {
    fontSize: 34,
  },
  titulo: {
    fontSize: 34,
    fontWeight: '800',
    color: cores.texto,
  },
  subtitulo: {
    fontSize: 15,
    color: cores.textoSecundario,
    marginTop: 2,
  },
  formulario: {
    backgroundColor: cores.superficie,
    borderRadius: raios.lg,
    borderWidth: 1,
    borderColor: cores.borda,
    padding: espacos.lg,
    ...sombraCard,
  },
  rotulo: {
    fontSize: 13,
    fontWeight: '700',
    color: '#334155',
    marginBottom: 6,
  },
  campo: {
    backgroundColor: cores.superficie,
    borderWidth: 1,
    borderColor: cores.borda,
    borderRadius: raios.md,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: cores.texto,
    marginBottom: espacos.md,
  },
  mensagem: {
    marginBottom: espacos.md,
  },
  botao: {
    backgroundColor: cores.marca,
    paddingVertical: 15,
    borderRadius: raios.md,
    alignItems: 'center',
  },
  botaoDesativado: {
    backgroundColor: '#7dd3fc',
  },
  botaoTexto: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '700',
  },
  ligacao: {
    marginTop: espacos.md,
    textAlign: 'center',
    fontSize: 15,
    fontWeight: '700',
    color: cores.marca,
  },
  ajuda: {
    marginTop: espacos.md,
    fontSize: 12,
    lineHeight: 18,
    color: cores.textoSecundario,
    textAlign: 'center',
  },
});
