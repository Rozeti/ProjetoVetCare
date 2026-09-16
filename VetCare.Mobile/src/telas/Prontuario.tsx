import { useCallback, useEffect, useState } from 'react';
import {
  Image,
  Linking,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { api, mensagemDeErro, urlDoArquivo } from '../services/api';
import type { ItemLinhaTempo, Prescricao, Prontuario as ProntuarioDTO, SituacaoDose, Vacina } from '../tipos';
import { Aviso, Cartao, Carregando, Etiqueta, SemDados } from '../componentes/ui';
import { Icone } from '../componentes/Icone';
import { GraficoBarras } from '../componentes/GraficoBarras';
import { AlertasClinicos } from '../componentes/AlertasClinicos';
import { cores, espacos, estiloEscalaDor, raios } from '../tema';
import { formatarData, formatarDataHora, formatarPeso } from '../utils/formato';

type Aba = 'historico' | 'evolucao' | 'vacinas' | 'receitas' | 'tratamentos' | 'documentos';

const ABAS: { valor: Aba; rotulo: string }[] = [
  { valor: 'historico', rotulo: 'Histórico' },
  { valor: 'evolucao', rotulo: 'Evolução' },
  { valor: 'vacinas', rotulo: 'Vacinação' },
  { valor: 'receitas', rotulo: 'Receitas' },
  { valor: 'tratamentos', rotulo: 'Tratamentos' },
  { valor: 'documentos', rotulo: 'Documentos' },
];

/** Cores da situação da dose, iguais às do portal web. */
const PALETA_DOSE: Record<SituacaoDose, { fundo: string; texto: string }> = {
  Vencida: { fundo: cores.perigoClaro, texto: '#991b1b' },
  'A vencer': { fundo: cores.alertaClaro, texto: '#92400e' },
  'Em dia': { fundo: cores.sucessoClaro, texto: '#065f46' },
  'Dose única': { fundo: cores.fundo, texto: cores.textoSecundario },
};

type Parametros = RouteProp<{ Prontuario: { pacienteId: string; nome?: string } }, 'Prontuario'>;

/**
 * HU-011 e HU-013, CA-2: prontuário do pet na visão do tutor.
 * RN-003: as observações internas não são enviadas pela API para este perfil,
 * então não há caminho por onde elas possam aparecer nesta tela.
 */
export function Prontuario() {
  const navegacao = useNavigation();
  const rota = useRoute<Parametros>();
  const { pacienteId, nome } = rota.params;

  const [prontuario, setProntuario] = useState<ProntuarioDTO | null>(null);
  const [aba, setAba] = useState<Aba>('historico');
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');

  const carregar = useCallback(async () => {
    setErro('');

    try {
      const { data } = await api.get<ProntuarioDTO>(`/api/prontuarios/paciente/${pacienteId}`);
      setProntuario(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível carregar o prontuário.'));
    } finally {
      setCarregando(false);
    }
  }, [pacienteId]);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return (
    <SafeAreaView style={estilos.area} edges={['top']}>
      <View style={estilos.cabecalho}>
        <TouchableOpacity
          onPress={() => navegacao.goBack()}
          style={estilos.voltar}
          accessibilityRole="button"
          accessibilityLabel="Voltar"
        >
          <Icone nome="voltar" tamanho={28} cor={cores.marca} />
        </TouchableOpacity>

        <Text style={estilos.tituloCabecalho} numberOfLines={1}>
          {prontuario?.nomePaciente ?? nome ?? 'Prontuário'}
        </Text>
      </View>

      {carregando ? (
        <Carregando texto="Carregando prontuário..." />
      ) : !prontuario ? (
        <View style={estilos.conteudo}>
          <Aviso tipo="erro">{erro || 'Prontuário não encontrado.'}</Aviso>
        </View>
      ) : (
        <ScrollView contentContainerStyle={estilos.conteudo}>
          {prontuario.alertasClinicos.length > 0 && (
            <View style={estilos.alertas}>
              <AlertasClinicos alertas={prontuario.alertasClinicos} />
            </View>
          )}

          <Cartao estilo={estilos.resumo}>
            <Text style={estilos.resumoTitulo}>{prontuario.nomePaciente}</Text>
            <Text style={estilos.resumoDetalhe}>
              {[
                prontuario.especie,
                prontuario.raca,
                prontuario.sexo,
                prontuario.castrado ? 'Castrado' : null,
              ]
                .filter(Boolean)
                .join(' · ')}
            </Text>

            <View style={estilos.resumoLinha}>
              <View style={estilos.resumoItem}>
                <Text style={estilos.resumoRotulo}>Idade</Text>
                <Text style={estilos.resumoValor}>{prontuario.idadeDescritiva}</Text>
              </View>

              <View style={estilos.resumoItem}>
                <Text style={estilos.resumoRotulo}>Peso atual</Text>
                <Text style={estilos.resumoValor}>
                  {formatarPeso(prontuario.evolucaoPeso.at(-1)?.valor)}
                </Text>
              </View>

              <View style={estilos.resumoItem}>
                <Text style={estilos.resumoRotulo}>Registros</Text>
                <Text style={estilos.resumoValor}>{prontuario.historico.length}</Text>
              </View>
            </View>
          </Cartao>

          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={estilos.abas}
          >
            {ABAS.map((item) => (
              <TouchableOpacity
                key={item.valor}
                onPress={() => setAba(item.valor)}
                style={[estilos.aba, aba === item.valor && estilos.abaAtiva]}
                accessibilityRole="tab"
                accessibilityState={{ selected: aba === item.valor }}
              >
                <Text style={[estilos.abaTexto, aba === item.valor && estilos.abaTextoAtivo]}>
                  {item.rotulo}
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>

          {aba === 'historico' && <Historico itens={prontuario.historico} />}

          {aba === 'evolucao' && (
            <>
              <Cartao estilo={estilos.cartaoGrafico}>
                <GraficoBarras titulo="Evolução de peso" pontos={prontuario.evolucaoPeso} unidade="kg" />
              </Cartao>

              <Cartao estilo={estilos.cartaoGrafico}>
                <GraficoBarras
                  titulo="Escala de dor"
                  pontos={prontuario.evolucaoDor}
                  cor={cores.perigo}
                  minimoFixo={0}
                  maximoFixo={10}
                />
              </Cartao>
            </>
          )}

          {aba === 'vacinas' && <Carteira vacinas={prontuario.vacinas} />}

          {aba === 'receitas' && <Receitas prescricoes={prontuario.prescricoes} />}

          {aba === 'tratamentos' &&
            (prontuario.tratamentos.length === 0 ? (
              <Cartao>
                <SemDados icone="🩺" titulo="Nenhum tratamento registrado" />
              </Cartao>
            ) : (
              prontuario.tratamentos.map((tratamento) => (
                <Cartao key={tratamento.id} estilo={estilos.cartaoItem}>
                  <Text style={estilos.objetivo}>{tratamento.objetivoTerapeutico}</Text>
                  <Text style={estilos.detalheSuave}>
                    {tratamento.nomeVeterinario} · {tratamento.status}
                  </Text>

                  <Text style={estilos.detalheSuave}>
                    {tratamento.sessoesConcluidas} de {tratamento.totalSessoes} sessões concluídas
                  </Text>

                  {tratamento.totalSessoes > 0 && (
                    <View style={estilos.progressoTrilha}>
                      <View
                        style={[
                          estilos.progressoBarra,
                          { width: `${(tratamento.sessoesConcluidas / tratamento.totalSessoes) * 100}%` },
                        ]}
                      />
                    </View>
                  )}
                </Cartao>
              ))
            ))}

          {aba === 'documentos' &&
            (prontuario.documentos.length === 0 ? (
              <Cartao>
                <SemDados
                  icone="📄"
                  titulo="Nenhum documento anexado"
                  descricao="Laudos, exames e contratos enviados pela clínica aparecem aqui."
                />
              </Cartao>
            ) : (
              prontuario.documentos.map((documento) => (
                <TouchableOpacity
                  key={documento.id}
                  activeOpacity={0.7}
                  onPress={() => Linking.openURL(urlDoArquivo(documento.urlArquivo))}
                  accessibilityRole="button"
                >
                  <Cartao estilo={estilos.cartaoDocumento}>
                    <Icone nome="documento" tamanho={24} />
                    <View style={estilos.documentoInfo}>
                      <Text style={estilos.documentoNome} numberOfLines={1}>
                        {documento.nomeArquivo}
                      </Text>
                      <Text style={estilos.detalheSuave}>
                        {documento.tipoDocumento} · {formatarDataHora(documento.dataUpload)}
                      </Text>
                    </View>
                    <Icone nome="seta" tamanho={24} cor={cores.textoSuave} />
                  </Cartao>
                </TouchableOpacity>
              ))
            ))}
        </ScrollView>
      )}
    </SafeAreaView>
  );
}

/** HU-011, CA-1: avaliações e atendimentos em ordem cronológica. */
function Historico({ itens }: { itens: ItemLinhaTempo[] }) {
  const [expandido, setExpandido] = useState<string | null>(null);

  if (itens.length === 0) {
    return (
      <Cartao>
        <SemDados
          icone="🩺"
          titulo="Prontuário ainda sem registros"
          descricao="Assim que a primeira avaliação ou atendimento for registrado, o histórico aparece aqui."
        />
      </Cartao>
    );
  }

  return (
    <>
      {itens.map((item) => {
        const aberto = expandido === item.id;
        const paletaDor = item.escalaDor != null ? estiloEscalaDor(item.escalaDor) : null;

        return (
          <Cartao key={item.id} estilo={estilos.cartaoItem}>
            <View style={estilos.itemTopo}>
              <Text style={estilos.itemTipo}>{item.tipo}</Text>

              <View style={estilos.itemEtiquetas}>
                {paletaDor && item.escalaDor != null && (
                  <Etiqueta texto={`Dor ${item.escalaDor}/10`} fundo={paletaDor.fundo} cor={paletaDor.texto} />
                )}
                {item.pesoKg != null && (
                  <Etiqueta texto={formatarPeso(item.pesoKg)} fundo={cores.fundo} cor={cores.textoSecundario} />
                )}
              </View>
            </View>

            <Text style={estilos.detalheSuave}>
              {formatarDataHora(item.data)}
              {item.autor ? ` · ${item.autor}` : ''}
            </Text>

            <Text style={estilos.itemDescricao}>{item.descricao}</Text>
            {item.detalhes ? <Text style={estilos.itemDetalhes}>{item.detalhes}</Text> : null}

            {aberto && Object.keys(item.campos).length > 0 && (
              <View style={estilos.campos}>
                {Object.entries(item.campos).map(([rotulo, valor]) => (
                  <View key={rotulo} style={estilos.campo}>
                    <Text style={estilos.campoRotulo}>{rotulo}</Text>
                    <Text style={estilos.campoValor}>{valor}</Text>
                  </View>
                ))}
              </View>
            )}

            {/* HU-011, CA-4: as mídias da sessão aparecem junto do registro. */}
            {item.midias.length > 0 && (
              <ScrollView horizontal showsHorizontalScrollIndicator={false} style={estilos.midias}>
                {item.midias.map((midia) => (
                  <TouchableOpacity
                    key={midia.id}
                    onPress={() => Linking.openURL(urlDoArquivo(midia.urlArquivo))}
                    accessibilityRole="imagebutton"
                    accessibilityLabel={midia.nomeArquivo}
                  >
                    {midia.tipo === 'Video' ? (
                      <View style={[estilos.midia, estilos.midiaVideo]}>
                        <Text style={estilos.midiaVideoTexto}>▶</Text>
                      </View>
                    ) : (
                      <Image source={{ uri: urlDoArquivo(midia.urlArquivo) }} style={estilos.midia} />
                    )}
                  </TouchableOpacity>
                ))}
              </ScrollView>
            )}

            {Object.keys(item.campos).length > 0 && (
              <TouchableOpacity
                onPress={() => setExpandido(aberto ? null : item.id)}
                accessibilityRole="button"
              >
                <Text style={estilos.verMais}>{aberto ? 'Recolher' : 'Ver detalhes'}</Text>
              </TouchableOpacity>
            )}
          </Cartao>
        );
      })}
    </>
  );
}

/** Carteira de vacinação do pet, com a situação de cada dose. */
function Carteira({ vacinas }: { vacinas: Vacina[] }) {
  if (vacinas.length === 0) {
    return (
      <Cartao>
        <SemDados
          icone="💉"
          titulo="Nenhuma aplicação registrada"
          descricao="Vacinas e vermífugos aplicados pela clínica aparecem aqui, com a data da próxima dose."
        />
      </Cartao>
    );
  }

  return (
    <>
      {vacinas.map((vacina) => {
        const paleta = PALETA_DOSE[vacina.situacaoDose] ?? PALETA_DOSE['Dose única'];

        return (
          <Cartao key={vacina.id} estilo={estilos.cartaoItem}>
            <View style={estilos.itemTopo}>
              <Text style={estilos.itemTipo}>{vacina.nome}</Text>
              <Etiqueta texto={vacina.situacaoDose} fundo={paleta.fundo} cor={paleta.texto} />
            </View>

            <Text style={estilos.detalheSuave}>
              {vacina.tipo} · aplicada em {formatarData(vacina.dataAplicacao)}
              {vacina.aplicadaPor ? ` · ${vacina.aplicadaPor}` : ''}
            </Text>

            {vacina.proximaDose ? (
              <Text style={estilos.itemDescricao}>
                Próxima dose em {formatarData(vacina.proximaDose)}
                {vacina.diasParaProximaDose != null &&
                  (vacina.diasParaProximaDose < 0
                    ? ` (${Math.abs(vacina.diasParaProximaDose)} dia(s) em atraso)`
                    : ` (em ${vacina.diasParaProximaDose} dia(s))`)}
              </Text>
            ) : null}

            {vacina.observacoes ? <Text style={estilos.itemDetalhes}>{vacina.observacoes}</Text> : null}
          </Cartao>
        );
      })}
    </>
  );
}

/** Receitas emitidas pela clínica, disponíveis para consulta a qualquer momento. */
function Receitas({ prescricoes }: { prescricoes: Prescricao[] }) {
  if (prescricoes.length === 0) {
    return (
      <Cartao>
        <SemDados
          icone="💊"
          titulo="Nenhuma receita emitida"
          descricao="As receitas do seu pet ficam guardadas aqui."
        />
      </Cartao>
    );
  }

  return (
    <>
      {prescricoes.map((prescricao) => (
        <Cartao key={prescricao.id} estilo={estilos.cartaoItem}>
          <View style={estilos.itemTopo}>
            <Text style={estilos.itemTipo}>Receita de {formatarData(prescricao.dataEmissao)}</Text>
            <Etiqueta
              texto={prescricao.status}
              fundo={prescricao.status === 'Ativa' ? cores.sucessoClaro : cores.fundo}
              cor={prescricao.status === 'Ativa' ? '#065f46' : cores.textoSecundario}
            />
          </View>

          <Text style={estilos.detalheSuave}>
            {prescricao.nomeVeterinario}
            {prescricao.crmv ? ` · ${prescricao.crmv}` : ''}
            {prescricao.validaAte ? ` · válida até ${formatarData(prescricao.validaAte)}` : ''}
          </Text>

          {prescricao.itens.map((item) => (
            <View key={item.id} style={estilos.medicamento}>
              <Text style={estilos.medicamentoNome}>{item.medicamento}</Text>
              <Text style={estilos.detalheSuave}>
                {[item.dosagem, item.frequencia, item.duracao, item.via].filter(Boolean).join(' · ')}
              </Text>
              {item.observacao ? <Text style={estilos.detalheSuave}>{item.observacao}</Text> : null}
            </View>
          ))}

          {prescricao.orientacoes ? (
            <Text style={estilos.itemDetalhes}>{prescricao.orientacoes}</Text>
          ) : null}
        </Cartao>
      ))}
    </>
  );
}

const estilos = StyleSheet.create({
  alertas: {
    marginBottom: espacos.md,
  },
  medicamento: {
    backgroundColor: cores.fundo,
    borderRadius: raios.md,
    padding: espacos.sm + 2,
    marginTop: espacos.sm,
    gap: 2,
  },
  medicamentoNome: {
    fontSize: 14,
    fontWeight: '700',
    color: cores.texto,
  },
  area: {
    flex: 1,
    backgroundColor: cores.fundo,
  },
  cabecalho: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.sm,
    paddingHorizontal: espacos.sm,
    paddingVertical: espacos.sm,
    backgroundColor: cores.superficie,
    borderBottomWidth: 1,
    borderBottomColor: cores.borda,
  },
  voltar: {
    padding: espacos.sm,
  },
  tituloCabecalho: {
    flex: 1,
    fontSize: 18,
    fontWeight: '700',
    color: cores.texto,
  },
  conteudo: {
    padding: espacos.md,
    paddingBottom: espacos.xl,
  },
  resumo: {
    marginBottom: espacos.md,
  },
  resumoTitulo: {
    fontSize: 20,
    fontWeight: '800',
    color: cores.texto,
  },
  resumoDetalhe: {
    fontSize: 13,
    color: cores.textoSecundario,
    marginTop: 2,
  },
  resumoLinha: {
    flexDirection: 'row',
    marginTop: espacos.md,
    backgroundColor: cores.fundo,
    borderRadius: raios.md,
    paddingVertical: espacos.sm + 2,
  },
  resumoItem: {
    flex: 1,
    alignItems: 'center',
    gap: 2,
  },
  resumoRotulo: {
    fontSize: 11,
    color: cores.textoSecundario,
  },
  resumoValor: {
    fontSize: 14,
    fontWeight: '700',
    color: cores.texto,
  },
  abas: {
    gap: espacos.sm,
    paddingBottom: espacos.md,
  },
  aba: {
    paddingHorizontal: espacos.md,
    paddingVertical: espacos.sm,
    borderRadius: raios.cheio,
    backgroundColor: cores.superficie,
    borderWidth: 1,
    borderColor: cores.borda,
  },
  abaAtiva: {
    backgroundColor: cores.marca,
    borderColor: cores.marca,
  },
  abaTexto: {
    fontSize: 13,
    fontWeight: '600',
    color: cores.textoSecundario,
  },
  abaTextoAtivo: {
    color: '#ffffff',
  },
  cartaoGrafico: {
    marginBottom: espacos.md,
  },
  cartaoItem: {
    marginBottom: espacos.md,
    gap: 4,
  },
  itemTopo: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: espacos.sm,
  },
  itemTipo: {
    fontSize: 15,
    fontWeight: '700',
    color: cores.texto,
  },
  itemEtiquetas: {
    flexDirection: 'row',
    gap: 6,
  },
  itemDescricao: {
    fontSize: 14,
    fontWeight: '600',
    color: '#334155',
    marginTop: 4,
  },
  itemDetalhes: {
    fontSize: 14,
    color: cores.textoSecundario,
    lineHeight: 20,
  },
  detalheSuave: {
    fontSize: 12,
    color: cores.textoSuave,
  },
  campos: {
    marginTop: espacos.sm,
    backgroundColor: cores.fundo,
    borderRadius: raios.md,
    padding: espacos.md,
    gap: espacos.sm,
  },
  campo: {
    gap: 2,
  },
  campoRotulo: {
    fontSize: 11,
    fontWeight: '700',
    color: cores.textoSecundario,
    textTransform: 'uppercase',
  },
  campoValor: {
    fontSize: 13,
    color: '#334155',
    lineHeight: 19,
  },
  midias: {
    marginTop: espacos.sm,
  },
  midia: {
    width: 88,
    height: 88,
    borderRadius: raios.md,
    marginRight: espacos.sm,
    backgroundColor: cores.fundo,
  },
  midiaVideo: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#0f172a',
  },
  midiaVideoTexto: {
    color: '#ffffff',
    fontSize: 24,
  },
  verMais: {
    marginTop: espacos.sm,
    fontSize: 13,
    fontWeight: '600',
    color: cores.marca,
  },
  objetivo: {
    fontSize: 15,
    fontWeight: '600',
    color: cores.texto,
    lineHeight: 21,
  },
  progressoTrilha: {
    height: 8,
    borderRadius: raios.cheio,
    backgroundColor: cores.fundo,
    overflow: 'hidden',
    marginTop: espacos.sm,
  },
  progressoBarra: {
    height: '100%',
    borderRadius: raios.cheio,
    backgroundColor: cores.marca,
  },
  cartaoDocumento: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: espacos.md,
    marginBottom: espacos.sm,
  },
  documentoInfo: {
    flex: 1,
    gap: 2,
  },
  documentoNome: {
    fontSize: 14,
    fontWeight: '600',
    color: cores.texto,
  },
});
