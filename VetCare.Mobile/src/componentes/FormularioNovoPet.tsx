import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api, mensagemDeErro } from '../services/api';
import type { Pet } from '../tipos';
import { Aviso, Botao, CampoDeMarcacao, CampoTexto, SeletorDeOpcao } from './ui';
import { Icone } from './Icone';
import { cores, espacos } from '../tema';
import { dataDigitadaParaIso, mascaraDeData } from '../utils/formato';

const ESPECIES = ['Cachorro', 'Gato', 'Ave', 'Roedor', 'Outro'] as const;
const SEXOS = ['Macho', 'Fêmea'] as const;

const FORM_VAZIO = {
  nome: '',
  especie: 'Cachorro' as string,
  raca: '',
  sexo: 'Macho' as string,
  pelagem: '',
  microchip: '',
  castrado: false,
  nascimento: '',
  peso: '',
};

/**
 * Cadastro do pet feito pelo próprio tutor, para o animal recém-adquirido que a clínica
 * ainda não conhece. O responsável não aparece no formulário: a API o deduz de quem está
 * autenticado (RN-001), de modo que ninguém cadastra um animal no nome de outra pessoa.
 */
export function FormularioNovoPet({
  aberto,
  aoFechar,
  aoCadastrar,
}: {
  aberto: boolean;
  aoFechar: () => void;
  aoCadastrar: (pet: Pet) => void;
}) {
  const [form, setForm] = useState(FORM_VAZIO);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function fechar() {
    setForm(FORM_VAZIO);
    setErro('');
    aoFechar();
  }

  async function enviar() {
    setErro('');

    if (!form.nome.trim()) {
      setErro('Informe o nome do seu pet.');
      return;
    }

    const nascimento = dataDigitadaParaIso(form.nascimento);

    if (!nascimento) {
      setErro('Informe a data de nascimento no formato DD/MM/AAAA.');
      return;
    }

    // A API também recusa datas futuras; avisar aqui evita uma ida ao servidor.
    if (new Date(nascimento) > new Date()) {
      setErro('A data de nascimento não pode ser futura.');
      return;
    }

    const peso = form.peso.trim().replace(',', '.');

    if (peso && (Number.isNaN(Number(peso)) || Number(peso) <= 0)) {
      setErro('Informe um peso válido, como 12,5.');
      return;
    }

    setSalvando(true);

    try {
      const { data } = await api.post<Pet>('/api/pets/meus', {
        nome: form.nome.trim(),
        especie: form.especie,
        raca: form.raca.trim(),
        sexo: form.sexo,
        pelagem: form.pelagem.trim(),
        microchip: form.microchip.trim(),
        castrado: form.castrado,
        dataNascimento: nascimento,
        pesoAtualKg: peso ? Number(peso) : null,
      });

      setForm(FORM_VAZIO);
      aoCadastrar(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível cadastrar seu pet.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal visible={aberto} animationType="slide" onRequestClose={fechar} transparent={false}>
      <SafeAreaView style={estilos.area} edges={['top', 'bottom']}>
        <View style={estilos.cabecalho}>
          <View style={estilos.tituloBloco}>
            <Text style={estilos.titulo}>Cadastrar meu pet</Text>
            <Text style={estilos.subtitulo}>
              Preencha o que souber. A equipe completa o cadastro na primeira consulta.
            </Text>
          </View>

          <TouchableOpacity
            onPress={fechar}
            style={estilos.fechar}
            accessibilityRole="button"
            accessibilityLabel="Fechar cadastro"
          >
            <Icone nome="cancelar" tamanho={16} cor={cores.textoSecundario} />
          </TouchableOpacity>
        </View>

        <KeyboardAvoidingView
          style={estilos.corpo}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          <ScrollView contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
            <CampoTexto
              rotulo="Nome do pet"
              obrigatorio
              valor={form.nome}
              aoMudar={(nome) => setForm({ ...form, nome })}
              exemplo="Ex.: Thor"
              maximo={80}
            />

            <SeletorDeOpcao
              rotulo="Espécie"
              obrigatorio
              opcoes={ESPECIES}
              selecionada={form.especie}
              aoSelecionar={(especie) => setForm({ ...form, especie })}
            />

            <CampoTexto
              rotulo="Raça"
              valor={form.raca}
              aoMudar={(raca) => setForm({ ...form, raca })}
              exemplo="Deixe em branco se não souber"
              maximo={80}
            />

            <SeletorDeOpcao
              rotulo="Sexo"
              opcoes={SEXOS}
              selecionada={form.sexo}
              aoSelecionar={(sexo) => setForm({ ...form, sexo })}
            />

            <CampoTexto
              rotulo="Data de nascimento"
              obrigatorio
              valor={form.nascimento}
              aoMudar={(texto) => setForm({ ...form, nascimento: mascaraDeData(texto) })}
              exemplo="DD/MM/AAAA"
              teclado="number-pad"
              dica="Se não souber a data exata, use a aproximada."
              maximo={10}
            />

            <CampoTexto
              rotulo="Peso atual (kg)"
              valor={form.peso}
              aoMudar={(peso) => setForm({ ...form, peso })}
              exemplo="Ex.: 12,5"
              teclado="decimal-pad"
              maximo={6}
            />

            <CampoTexto
              rotulo="Pelagem"
              valor={form.pelagem}
              aoMudar={(pelagem) => setForm({ ...form, pelagem })}
              exemplo="Ex.: caramelo curto"
              maximo={60}
            />

            <CampoTexto
              rotulo="Microchip"
              valor={form.microchip}
              aoMudar={(microchip) => setForm({ ...form, microchip })}
              dica="Número da identificação eletrônica, se o animal tiver."
              teclado="number-pad"
              maximo={40}
            />

            <CampoDeMarcacao
              rotulo="Meu pet é castrado"
              marcado={form.castrado}
              aoAlternar={(castrado) => setForm({ ...form, castrado })}
            />

            {erro ? (
              <View style={estilos.erro}>
                <Aviso tipo="erro">{erro}</Aviso>
              </View>
            ) : null}
          </ScrollView>

          <View style={estilos.acoes}>
            <Botao
              titulo="Cancelar"
              variante="secundario"
              aoPressionar={fechar}
              desabilitado={salvando}
              estilo={estilos.acao}
            />
            <Botao
              titulo="Cadastrar pet"
              aoPressionar={enviar}
              carregando={salvando}
              estilo={estilos.acao}
            />
          </View>
        </KeyboardAvoidingView>
      </SafeAreaView>
    </Modal>
  );
}

const estilos = StyleSheet.create({
  area: {
    flex: 1,
    backgroundColor: cores.fundo,
  },
  cabecalho: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: espacos.md,
    paddingHorizontal: espacos.md,
    paddingTop: espacos.sm,
    paddingBottom: espacos.md,
    borderBottomWidth: 1,
    borderBottomColor: cores.borda,
    backgroundColor: cores.superficie,
  },
  tituloBloco: {
    flex: 1,
  },
  titulo: {
    fontSize: 20,
    fontWeight: '800',
    color: cores.texto,
  },
  subtitulo: {
    fontSize: 13,
    color: cores.textoSecundario,
    marginTop: 2,
    lineHeight: 18,
  },
  fechar: {
    width: 34,
    height: 34,
    borderRadius: 17,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: cores.fundo,
  },
  corpo: {
    flex: 1,
  },
  conteudo: {
    padding: espacos.md,
    paddingBottom: espacos.lg,
  },
  erro: {
    marginTop: espacos.sm,
  },
  acoes: {
    flexDirection: 'row',
    gap: espacos.sm,
    padding: espacos.md,
    borderTopWidth: 1,
    borderTopColor: cores.borda,
    backgroundColor: cores.superficie,
  },
  acao: {
    flex: 1,
  },
});
