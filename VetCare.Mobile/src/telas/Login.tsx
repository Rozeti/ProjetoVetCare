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
import { useAuth } from '../contextos/AuthContext';
import { mensagemDeErro } from '../services/api';
import { Aviso } from '../componentes/ui';
import { cores, espacos, raios, sombraCard } from '../tema';

/** HU-001: autenticação do tutor no aplicativo. */
export function Login() {
  const { entrar } = useAuth();

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState('');
  const [enviando, setEnviando] = useState(false);

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
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
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
            editable={!enviando}
            onSubmitEditing={aoEntrar}
            returnKeyType="go"
          />

          {erro ? (
            <View style={estilos.erro}>
              <Aviso tipo="erro">{erro}</Aviso>
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

          <Text style={estilos.ajuda}>
            Esqueceu a senha? A redefinição é feita pela clínica, que gera uma senha provisória
            para o seu acesso.
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
  erro: {
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
  ajuda: {
    marginTop: espacos.md,
    fontSize: 12,
    lineHeight: 18,
    color: cores.textoSecundario,
    textAlign: 'center',
  },
});
