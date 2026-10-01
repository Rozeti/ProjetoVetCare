import { useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Linking,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api, mensagemDeErro, URL_API } from '../services/api';
import { useAuth } from '../contextos/AuthContext';
import type { Usuario } from '../tipos';
import { Alerta, Avatar, Cartao } from '../componentes/ui';
import { cores, espacos, raios } from '../tema';
import { formatarData, formatarDataHora } from '../utils/formato';

const ROTULO_PERFIL: Record<Usuario['perfil'], string> = {
  Administrador: 'Administrador(a)',
  Veterinario: 'Veterinário(a)',
  Tutor: 'Tutor(a)',
  Apoio: 'Equipe de apoio',
};

/** Dados da conta do tutor, canais de aviso, troca de senha e saída do aplicativo. */
export function Perfil() {
  const { usuario, sair, atualizarUsuario, push, reativarPush } = useAuth();

  const [telefone, setTelefone] = useState(usuario?.telefone ?? '');
  const [endereco, setEndereco] = useState(usuario?.endereco ?? '');
  const [salvandoContato, setSalvandoContato] = useState(false);
  const [erroContato, setErroContato] = useState('');
  const [sucessoContato, setSucessoContato] = useState('');

  const [salvandoPreferencias, setSalvandoPreferencias] = useState(false);
  const [erroPreferencias, setErroPreferencias] = useState('');

  const [senhaAtual, setSenhaAtual] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erroSenha, setErroSenha] = useState('');
  const [sucessoSenha, setSucessoSenha] = useState('');
  const [salvandoSenha, setSalvandoSenha] = useState(false);

  if (!usuario) return null;

  const contatoAlterado = telefone !== (usuario.telefone ?? '') || endereco !== (usuario.endereco ?? '');

  /** HU-015: o tutor escolhe por onde quer ser avisado além do próprio aplicativo. */
  async function alterarPreferencia(campo: 'notificarPorEmail' | 'notificarPorPush', valor: boolean) {
    if (!usuario) return;

    setErroPreferencias('');
    setSalvandoPreferencias(true);

    const preferencias = {
      notificarPorEmail: usuario.notificarPorEmail,
      notificarPorPush: usuario.notificarPorPush,
      [campo]: valor,
    };

    try {
      const { data } = await api.put<Usuario>('/api/usuarios/me/preferencias-de-notificacao', preferencias);
      atualizarUsuario(data);

      if (campo === 'notificarPorPush' && valor && push.estado !== 'ativo') {
        reativarPush();
      }
    } catch (falha) {
      setErroPreferencias(mensagemDeErro(falha, 'Não foi possível salvar a preferência.'));
    } finally {
      setSalvandoPreferencias(false);
    }
  }

  async function salvarContato() {
    if (!usuario?.tutorId) return;

    setErroContato('');
    setSucessoContato('');
    setSalvandoContato(true);

    try {
      await api.put(`/api/tutores/${usuario.tutorId}`, {
        telefone: telefone.trim() || null,
        endereco: endereco.trim() || null,
      });

      const { data } = await api.get<Usuario>('/api/usuarios/me');
      atualizarUsuario(data);
      setSucessoContato('Dados de contato atualizados.');
    } catch (falha) {
      setErroContato(mensagemDeErro(falha, 'Não foi possível atualizar o contato.'));
    } finally {
      setSalvandoContato(false);
    }
  }

  async function alterarSenha() {
    setErroSenha('');
    setSucessoSenha('');

    if (novaSenha.length < 6) {
      setErroSenha('A nova senha deve ter no mínimo 6 caracteres.');
      return;
    }

    if (novaSenha !== confirmacao) {
      setErroSenha('A confirmação não confere com a nova senha.');
      return;
    }

    setSalvandoSenha(true);

    try {
      await api.put('/api/usuarios/me/senha', { senhaAtual, novaSenha });

      setSucessoSenha('Senha alterada com sucesso. Um aviso de segurança foi enviado ao seu e-mail.');
      setSenhaAtual('');
      setNovaSenha('');
      setConfirmacao('');
    } catch (falha) {
      setErroSenha(mensagemDeErro(falha, 'Não foi possível alterar a senha.'));
    } finally {
      setSalvandoSenha(false);
    }
  }

  // HU-001, CA-4: o logout encerra a sessão e devolve o usuário à tela de login.
  function confirmarSaida() {
    Alert.alert('Sair do aplicativo', 'Deseja encerrar a sessão?', [
      { text: 'Continuar conectado', style: 'cancel' },
      { text: 'Sair', style: 'destructive', onPress: () => sair() },
    ]);
  }

  return (
    <SafeAreaView style={estilos.area} edges={['top']}>
      <ScrollView contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
        <Text style={estilos.titulo}>Minha conta</Text>

        <Cartao estilo={estilos.cartaoPerfil}>
          <Avatar nome={usuario.nome} tamanho={60} />

          <View style={estilos.identificacao}>
            <Text style={estilos.nome}>{usuario.nome}</Text>
            <Text style={estilos.email}>{usuario.email}</Text>
          </View>
        </Cartao>

        <Cartao estilo={estilos.cartao}>
          <Linha rotulo="Perfil" valor={ROTULO_PERFIL[usuario.perfil] ?? usuario.perfil} />
          {usuario.cpf ? <Linha rotulo="CPF" valor={usuario.cpf} /> : null}
          <Linha rotulo="Cadastro" valor={formatarData(usuario.dataCadastro)} />
          {usuario.ultimoAcesso ? (
            <Linha rotulo="Último acesso" valor={formatarDataHora(usuario.ultimoAcesso)} />
          ) : null}
        </Cartao>

        <Text style={estilos.secao}>Como quero ser avisado</Text>

        <Cartao estilo={estilos.cartao}>
          <Text style={estilos.explicacao}>
            Os avisos de sessões, lembretes, registros no prontuário e mensagens aparecem sempre na aba
            "Avisos". Escolha se quer recebê-los também por e-mail e no celular.
          </Text>

          <Preferencia
            rotulo="E-mail"
            descricao={`Enviar para ${usuario.email}`}
            ativo={usuario.notificarPorEmail}
            aoAlternar={(valor) => alterarPreferencia('notificarPorEmail', valor)}
            desabilitado={salvandoPreferencias}
          />

          <Preferencia
            rotulo="Notificações no celular"
            descricao={descricaoDoPush(push.estado, push.motivo)}
            ativo={usuario.notificarPorPush}
            aoAlternar={(valor) => alterarPreferencia('notificarPorPush', valor)}
            desabilitado={salvandoPreferencias}
          />

          {usuario.notificarPorPush && push.estado === 'sem-permissao' ? (
            <TouchableOpacity onPress={() => Linking.openSettings()} accessibilityRole="button">
              <Text style={estilos.ligacao}>Abrir as configurações do celular</Text>
            </TouchableOpacity>
          ) : null}

          {usuario.notificarPorPush && (push.estado === 'erro' || push.estado === 'desconhecido') ? (
            <TouchableOpacity onPress={reativarPush} accessibilityRole="button">
              <Text style={estilos.ligacao}>Tentar ativar de novo</Text>
            </TouchableOpacity>
          ) : null}

          {erroPreferencias ? <Alerta tipo="erro">{erroPreferencias}</Alerta> : null}
        </Cartao>

        <Text style={estilos.secao}>Contato</Text>

        <Cartao estilo={estilos.cartao}>
          <Text style={estilos.explicacao}>
            A clínica usa estes dados para falar com você sobre o tratamento dos seus pets.
          </Text>

          <Text style={estilos.rotulo}>Telefone</Text>
          <TextInput
            style={estilos.campo}
            value={telefone}
            onChangeText={setTelefone}
            keyboardType="phone-pad"
            autoComplete="tel"
            placeholder="(00) 00000-0000"
            placeholderTextColor={cores.textoSuave}
            editable={!salvandoContato}
            accessibilityLabel="Telefone"
          />

          <Text style={estilos.rotulo}>Endereço</Text>
          <TextInput
            style={estilos.campo}
            value={endereco}
            onChangeText={setEndereco}
            autoComplete="street-address"
            placeholder="Rua, número, bairro, cidade"
            placeholderTextColor={cores.textoSuave}
            editable={!salvandoContato}
            accessibilityLabel="Endereço"
          />

          {erroContato ? <Alerta tipo="erro">{erroContato}</Alerta> : null}
          {sucessoContato ? <Alerta tipo="sucesso">{sucessoContato}</Alerta> : null}

          <TouchableOpacity
            style={[estilos.botaoPrimario, (salvandoContato || !contatoAlterado) && estilos.botaoDesativado]}
            onPress={salvarContato}
            disabled={salvandoContato || !contatoAlterado}
            accessibilityRole="button"
          >
            {salvandoContato ? (
              <ActivityIndicator color="#ffffff" />
            ) : (
              <Text style={estilos.botaoPrimarioTexto}>Salvar contato</Text>
            )}
          </TouchableOpacity>
        </Cartao>

        <Text style={estilos.secao}>Alterar senha</Text>

        <Cartao estilo={estilos.cartao}>
          <Text style={estilos.rotulo}>Senha atual</Text>
          <TextInput
            style={estilos.campo}
            secureTextEntry
            value={senhaAtual}
            onChangeText={setSenhaAtual}
            editable={!salvandoSenha}
            autoComplete="current-password"
            placeholderTextColor={cores.textoSuave}
            accessibilityLabel="Senha atual"
          />

          <Text style={estilos.rotulo}>Nova senha</Text>
          <TextInput
            style={estilos.campo}
            secureTextEntry
            value={novaSenha}
            onChangeText={setNovaSenha}
            editable={!salvandoSenha}
            autoComplete="new-password"
            placeholder="Mínimo de 6 caracteres"
            placeholderTextColor={cores.textoSuave}
            accessibilityLabel="Nova senha"
          />

          <Text style={estilos.rotulo}>Confirmar nova senha</Text>
          <TextInput
            style={estilos.campo}
            secureTextEntry
            value={confirmacao}
            onChangeText={setConfirmacao}
            editable={!salvandoSenha}
            autoComplete="new-password"
            placeholderTextColor={cores.textoSuave}
            accessibilityLabel="Confirmar nova senha"
          />

          {erroSenha ? <Alerta tipo="erro">{erroSenha}</Alerta> : null}
          {sucessoSenha ? <Alerta tipo="sucesso">{sucessoSenha}</Alerta> : null}

          <TouchableOpacity
            style={[estilos.botaoPrimario, salvandoSenha && estilos.botaoDesativado]}
            onPress={alterarSenha}
            disabled={salvandoSenha}
            accessibilityRole="button"
          >
            {salvandoSenha ? (
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
          VetCare — aplicativo do tutor{'\n'}
          Conectado a {URL_API}
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}

/** Texto curto que explica em que pé está o push neste aparelho. */
function descricaoDoPush(estado: string, motivo?: string): string {
  switch (estado) {
    case 'ativo':
      return 'Este celular está registrado para receber os avisos.';
    case 'sem-permissao':
      return 'Permissão negada no celular. Permita as notificações do VetCare nas configurações.';
    case 'indisponivel':
    case 'nao-configurado':
    case 'erro':
      return motivo ?? 'Não foi possível ativar as notificações neste aparelho.';
    default:
      return 'Ativando as notificações neste aparelho...';
  }
}

function Preferencia({
  rotulo,
  descricao,
  ativo,
  aoAlternar,
  desabilitado,
}: {
  rotulo: string;
  descricao: string;
  ativo: boolean;
  aoAlternar: (valor: boolean) => void;
  desabilitado?: boolean;
}) {
  return (
    <View style={estilos.preferencia}>
      <View style={estilos.preferenciaTexto}>
        <Text style={estilos.preferenciaRotulo}>{rotulo}</Text>
        <Text style={estilos.preferenciaDescricao}>{descricao}</Text>
      </View>

      <Switch
        value={ativo}
        onValueChange={aoAlternar}
        disabled={desabilitado}
        trackColor={{ false: cores.borda, true: '#7dd3fc' }}
        thumbColor={ativo ? cores.marca : '#f1f5f9'}
        accessibilityLabel={rotulo}
      />
    </View>
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
  explicacao: {
    fontSize: 13,
    lineHeight: 19,
    color: cores.textoSecundario,
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
  preferencia: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.md,
    paddingVertical: 6,
  },
  preferenciaTexto: {
    flex: 1,
    gap: 2,
  },
  preferenciaRotulo: {
    fontSize: 15,
    fontWeight: '700',
    color: cores.texto,
  },
  preferenciaDescricao: {
    fontSize: 12,
    lineHeight: 17,
    color: cores.textoSecundario,
  },
  ligacao: {
    fontSize: 13,
    fontWeight: '700',
    color: cores.marca,
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
