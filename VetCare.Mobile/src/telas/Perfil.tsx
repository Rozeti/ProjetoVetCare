import { useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api, mensagemDeErro, URL_API } from '../services/api';
import { useAuth } from '../contextos/AuthContext';
import { Avatar, Aviso, Cartao } from '../componentes/ui';
import { cores, espacos, raios } from '../tema';
import { formatarData, formatarDataHora } from '../utils/formato';

/** Dados da conta do tutor, troca de senha e saída do aplicativo. */
export function Perfil() {
  const { usuario, sair } = useAuth();

  const [senhaAtual, setSenhaAtual] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function alterarSenha() {
    setErro('');
    setAviso('');

    if (novaSenha.length < 6) {
      setErro('A nova senha deve ter no mínimo 6 caracteres.');
      return;
    }

    if (novaSenha !== confirmacao) {
      setErro('A confirmação não confere com a nova senha.');
      return;
    }

    setSalvando(true);

    try {
      await api.put('/api/usuarios/me/senha', { senhaAtual, novaSenha });

      setAviso('Senha alterada com sucesso.');
      setSenhaAtual('');
      setNovaSenha('');
      setConfirmacao('');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível alterar a senha.'));
    } finally {
      setSalvando(false);
    }
  }

  // HU-001, CA-4: o logout encerra a sessão e devolve o usuário à tela de login.
  function confirmarSaida() {
    Alert.alert('Sair do aplicativo', 'Deseja encerrar a sessão?', [
      { text: 'Continuar conectado', style: 'cancel' },
      { text: 'Sair', style: 'destructive', onPress: () => sair() },
    ]);
  }

  if (!usuario) return null;

  return (
    <SafeAreaView style={estilos.area} edges={['top']}>
      <ScrollView contentContainerStyle={estilos.conteudo}>
        <Text style={estilos.titulo}>Minha conta</Text>

        <Cartao estilo={estilos.cartaoPerfil}>
          <Avatar nome={usuario.nome} tamanho={60} />

          <View style={estilos.identificacao}>
            <Text style={estilos.nome}>{usuario.nome}</Text>
            <Text style={estilos.email}>{usuario.email}</Text>
          </View>
        </Cartao>

        <Cartao estilo={estilos.cartao}>
          <Linha rotulo="Perfil" valor="Tutor(a)" />
          {usuario.telefone ? <Linha rotulo="Telefone" valor={usuario.telefone} /> : null}
          {usuario.endereco ? <Linha rotulo="Endereço" valor={usuario.endereco} /> : null}
          <Linha rotulo="Cadastro" valor={formatarData(usuario.dataCadastro)} />
          {usuario.ultimoAcesso ? (
            <Linha rotulo="Último acesso" valor={formatarDataHora(usuario.ultimoAcesso)} />
          ) : null}
        </Cartao>

        <Text style={estilos.secao}>Alterar senha</Text>

        <Cartao estilo={estilos.cartao}>
          <Text style={estilos.rotulo}>Senha atual</Text>
          <TextInput
            style={estilos.campo}
            secureTextEntry
            value={senhaAtual}
            onChangeText={setSenhaAtual}
            editable={!salvando}
            placeholderTextColor={cores.textoSuave}
          />

          <Text style={estilos.rotulo}>Nova senha</Text>
          <TextInput
            style={estilos.campo}
            secureTextEntry
            value={novaSenha}
            onChangeText={setNovaSenha}
            editable={!salvando}
            placeholder="Mínimo de 6 caracteres"
            placeholderTextColor={cores.textoSuave}
          />

          <Text style={estilos.rotulo}>Confirmar nova senha</Text>
          <TextInput
            style={estilos.campo}
            secureTextEntry
            value={confirmacao}
            onChangeText={setConfirmacao}
            editable={!salvando}
            placeholderTextColor={cores.textoSuave}
          />

          {erro ? <Aviso tipo="erro">{erro}</Aviso> : null}
          {aviso ? <Aviso tipo="sucesso">{aviso}</Aviso> : null}

          <TouchableOpacity
            style={[estilos.botaoPrimario, salvando && estilos.botaoDesativado]}
            onPress={alterarSenha}
            disabled={salvando}
            accessibilityRole="button"
          >
            {salvando ? (
              <ActivityIndicator color="#ffffff" />
            ) : (
              <Text style={estilos.botaoPrimarioTexto}>Alterar senha</Text>
            )}
          </TouchableOpacity>
        </Cartao>

        <TouchableOpacity style={estilos.botaoSair} onPress={confirmarSaida} accessibilityRole="button">
          <Text style={estilos.botaoSairTexto}>Sair do aplicativo</Text>
        </TouchableOpacity>

        <Text style={estilos.rodape}>
          VetCare — Clínica VetSPA{'\n'}
          Conectado a {URL_API}
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <View style={estilos.linha}>
      <Text style={estilos.linhaRotulo}>{rotulo}</Text>
      <Text style={estilos.linhaValor}>{valor}</Text>
    </View>
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
    marginBottom: espacos.md,
  },
  cartaoPerfil: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.md,
    marginBottom: espacos.md,
  },
  identificacao: {
    flex: 1,
    gap: 2,
  },
  nome: {
    fontSize: 18,
    fontWeight: '700',
    color: cores.texto,
  },
  email: {
    fontSize: 13,
    color: cores.textoSecundario,
  },
  cartao: {
    marginBottom: espacos.md,
    gap: espacos.sm,
  },
  linha: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: espacos.md,
    paddingVertical: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: cores.borda,
  },
  linhaRotulo: {
    fontSize: 13,
    color: cores.textoSecundario,
  },
  linhaValor: {
    flex: 1,
    fontSize: 13,
    fontWeight: '600',
    color: cores.texto,
    textAlign: 'right',
  },
  secao: {
    fontSize: 13,
    fontWeight: '700',
    color: cores.textoSecundario,
    textTransform: 'uppercase',
    marginBottom: espacos.sm,
  },
  rotulo: {
    fontSize: 13,
    fontWeight: '700',
    color: '#334155',
  },
  campo: {
    borderWidth: 1,
    borderColor: cores.borda,
    borderRadius: raios.md,
    paddingHorizontal: espacos.md,
    paddingVertical: 11,
    fontSize: 15,
    color: cores.texto,
    backgroundColor: cores.fundo,
  },
  botaoPrimario: {
    backgroundColor: cores.marca,
    paddingVertical: 13,
    borderRadius: raios.md,
    alignItems: 'center',
    marginTop: espacos.sm,
  },
  botaoPrimarioTexto: {
    color: '#ffffff',
    fontSize: 15,
    fontWeight: '700',
  },
  botaoDesativado: {
    opacity: 0.6,
  },
  botaoSair: {
    backgroundColor: cores.perigoClaro,
    paddingVertical: 14,
    borderRadius: raios.md,
    alignItems: 'center',
  },
  botaoSairTexto: {
    color: cores.perigo,
    fontSize: 15,
    fontWeight: '700',
  },
  rodape: {
    marginTop: espacos.lg,
    fontSize: 11,
    color: cores.textoSuave,
    textAlign: 'center',
    lineHeight: 17,
  },
});
