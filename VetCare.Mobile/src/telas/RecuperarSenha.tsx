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
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, type NavigationProp } from '@react-navigation/native';
import { api, mensagemDeErro } from '../services/api';
import type { RespostaRecuperacao } from '../tipos';
import type { RotasDaPilha } from '../navegacao/rotas';
import { Alerta } from '../componentes/ui';
import { Icone } from '../componentes/Icone';
import { cores, espacos, raios, sombraCard } from '../tema';

type Etapa = 'solicitar' | 'redefinir';

/**
 * "Esqueci minha senha" no aplicativo. O tutor informa o e-mail cadastrado, recebe um
 * código de seis dígitos (e um link, para quem preferir o portal) e define a nova
 * senha aqui mesmo. O mesmo caminho conclui o primeiro acesso de uma conta criada
 * pela clínica.
 */
export function RecuperarSenha() {
  const navegacao = useNavigation<NavigationProp<RotasDaPilha>>();

  const [etapa, setEtapa] = useState<Etapa>('solicitar');
  const [email, setEmail] = useState('');
  const [codigo, setCodigo] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [codigoPreenchidoEmDesenvolvimento, setCodigoPreenchidoEmDesenvolvimento] = useState(false);
  const [enviando, setEnviando] = useState(false);

  async function solicitar() {
    setErro('');
    setAviso('');

    if (!email.trim()) {
      setErro('Informe o e-mail da sua conta.');
      return;
    }

    setEnviando(true);

    try {
      const { data } = await api.post<RespostaRecuperacao>('/api/usuarios/recuperar-senha', {
        email: email.trim(),
      });

      setAviso(data.mensagem);

      // Sem servidor de e-mail configurado, a API de desenvolvimento devolve o código
      // na resposta para o fluxo poder ser percorrido de ponta a ponta.
      if (data.codigoDesenvolvimento) {
        setCodigo(data.codigoDesenvolvimento);
        setCodigoPreenchidoEmDesenvolvimento(true);
      }

      setEtapa('redefinir');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível processar a solicitação.'));
    } finally {
      setEnviando(false);
    }
  }

  async function redefinir() {
    setErro('');

    const digitos = codigo.replace(/\D/g, '');

    if (digitos.length !== 6) {
      setErro('Digite o código de 6 dígitos recebido por e-mail.');
      return;
    }

    if (novaSenha.length < 6) {
      setErro('A nova senha deve ter no mínimo 6 caracteres.');
      return;
    }

    if (novaSenha !== confirmacao) {
      setErro('A confirmação não confere com a nova senha.');
      return;
    }

    setEnviando(true);

    try {
      const { data } = await api.post<{ mensagem: string }>('/api/usuarios/redefinir-senha', {
        email: email.trim(),
        codigo: digitos,
        novaSenha,
      });

      // De volta ao login, já com a confirmação na tela.
      navegacao.navigate('Login', { mensagem: data.mensagem || 'Senha redefinida com sucesso. Use-a para entrar.' });
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível redefinir a senha.'));
    } finally {
      setEnviando(false);
    }
  }

  function voltarParaSolicitar() {
    setEtapa('solicitar');
    setErro('');
    setAviso('');
    setCodigo('');
    setCodigoPreenchidoEmDesenvolvimento(false);
  }

  return (
    <SafeAreaView style={estilos.area} edges={['top', 'bottom']}>
      <KeyboardAvoidingView style={estilos.flex} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <ScrollView contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
          <TouchableOpacity
            onPress={() => navegacao.goBack()}
            style={estilos.voltar}
            accessibilityRole="button"
            accessibilityLabel="Voltar para o login"
          >
            <Icone nome="voltar" tamanho={26} cor={cores.marca} />
            <Text style={estilos.voltarTexto}>Voltar para o login</Text>
          </TouchableOpacity>

          <View style={estilos.formulario}>
            {etapa === 'solicitar' ? (
              <>
                <Text style={estilos.titulo}>Esqueceu a senha?</Text>
                <Text style={estilos.subtitulo}>
                  Informe o e-mail da sua conta. Você receberá um código de 6 dígitos para definir uma nova senha.
                </Text>

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
                  onSubmitEditing={solicitar}
                  returnKeyType="send"
                />

                {erro ? (
                  <View style={estilos.mensagem}>
                    <Alerta tipo="erro">{erro}</Alerta>
                  </View>
                ) : null}

                <TouchableOpacity
                  style={[estilos.botao, enviando && estilos.botaoDesativado]}
                  onPress={solicitar}
                  disabled={enviando}
                  accessibilityRole="button"
                >
                  {enviando ? <ActivityIndicator color="#ffffff" /> : <Text style={estilos.botaoTexto}>Enviar código</Text>}
                </TouchableOpacity>
              </>
            ) : (
              <>
                <Text style={estilos.titulo}>Definir nova senha</Text>

                <View style={estilos.mensagem}>
                  <Alerta tipo="info">
                    {aviso || 'Se houver uma conta com este e-mail, enviamos as instruções.'} Abra o e-mail e
                    digite abaixo o código de 6 dígitos.
                  </Alerta>
                </View>

                <Text style={estilos.rotulo}>Código recebido por e-mail</Text>
                <TextInput
                  style={[estilos.campo, estilos.campoCodigo]}
                  placeholder="000000"
                  placeholderTextColor={cores.textoSuave}
                  value={codigo}
                  onChangeText={setCodigo}
                  keyboardType="number-pad"
                  autoComplete="one-time-code"
                  textContentType="oneTimeCode"
                  maxLength={7}
                  editable={!enviando}
                />
                {codigoPreenchidoEmDesenvolvimento ? (
                  <Text style={estilos.dica}>
                    Ambiente de desenvolvimento sem servidor de e-mail: o código foi preenchido automaticamente.
                  </Text>
                ) : null}

                <Text style={estilos.rotulo}>Nova senha</Text>
                <TextInput
                  style={estilos.campo}
                  placeholder="Mínimo de 6 caracteres"
                  placeholderTextColor={cores.textoSuave}
                  value={novaSenha}
                  onChangeText={setNovaSenha}
                  secureTextEntry
                  autoComplete="new-password"
                  editable={!enviando}
                />

                <Text style={estilos.rotulo}>Confirmar nova senha</Text>
                <TextInput
                  style={estilos.campo}
                  placeholderTextColor={cores.textoSuave}
                  value={confirmacao}
                  onChangeText={setConfirmacao}
                  secureTextEntry
                  autoComplete="new-password"
                  editable={!enviando}
                  onSubmitEditing={redefinir}
                  returnKeyType="done"
                />

                {erro ? (
                  <View style={estilos.mensagem}>
                    <Alerta tipo="erro">{erro}</Alerta>
                  </View>
                ) : null}

                <TouchableOpacity
                  style={[estilos.botao, enviando && estilos.botaoDesativado]}
                  onPress={redefinir}
                  disabled={enviando}
                  accessibilityRole="button"
                >
                  {enviando ? <ActivityIndicator color="#ffffff" /> : <Text style={estilos.botaoTexto}>Redefinir senha</Text>}
                </TouchableOpacity>

                <TouchableOpacity onPress={voltarParaSolicitar} disabled={enviando} accessibilityRole="button">
                  <Text style={estilos.ligacao}>Não recebeu? Enviar novamente</Text>
                </TouchableOpacity>
              </>
            )}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
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
    flexGrow: 1,
    padding: espacos.lg,
    justifyContent: 'center',
  },
  voltar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    marginBottom: espacos.md,
  },
  voltarTexto: {
    fontSize: 14,
    fontWeight: '600',
    color: cores.marca,
  },
  formulario: {
    backgroundColor: cores.superficie,
    borderRadius: raios.lg,
    borderWidth: 1,
    borderColor: cores.borda,
    padding: espacos.lg,
    ...sombraCard,
  },
  titulo: {
    fontSize: 22,
    fontWeight: '800',
    color: cores.texto,
  },
  subtitulo: {
    fontSize: 14,
    lineHeight: 20,
    color: cores.textoSecundario,
    marginTop: 4,
    marginBottom: espacos.md,
  },
  rotulo: {
    fontSize: 13,
    fontWeight: '700',
    color: '#334155',
    marginBottom: 6,
    marginTop: espacos.sm,
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
    marginBottom: espacos.sm,
  },
  campoCodigo: {
    textAlign: 'center',
    fontSize: 22,
    letterSpacing: 8,
    fontWeight: '700',
  },
  dica: {
    fontSize: 12,
    color: cores.textoSuave,
    marginBottom: espacos.sm,
  },
  mensagem: {
    marginVertical: espacos.sm,
  },
  botao: {
    backgroundColor: cores.marca,
    paddingVertical: 15,
    borderRadius: raios.md,
    alignItems: 'center',
    marginTop: espacos.sm,
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
    fontSize: 14,
    fontWeight: '600',
    color: cores.marca,
  },
});
