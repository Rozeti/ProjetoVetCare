import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  Activity,
  ArrowLeft,
  ClipboardList,
  ClipboardPlus,
  Download,
  FileText,
  FlaskConical,
  Loader2,
  Paperclip,
  Pencil,
  Pill,
  Printer,
  ShieldAlert,
  Stethoscope,
  Syringe,
} from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAuth } from '../contexts/auth';
import { useCarregamento } from '../hooks/useCarregamento';
import { useConfirmacao } from '../hooks/useConfirmacao';
import { useAtualizacao } from '../contexts/atualizacoes';
import type {
  Atendimento,
  Avaliacao,
  Documento,
  ItemLinhaTempo,
  PedidoExame,
  Pet,
  Prontuario as ProntuarioDTO,
  Sessao,
  SessaoParaRegistro,
  Tratamento,
} from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, SemDados } from '../components/ui';
import { formatarData, formatarDia, formatarTamanho } from '../utils/formato';
import { AlertasClinicos } from '../components/AlertasClinicos';
import { imprimirProntuario } from '../utils/impressao';
import { ModalAvaliacao } from './componentes/ModalAvaliacao';
import { ModalRegistrarAtendimento } from './componentes/ModalRegistrarAtendimento';
import { ModalTratamento } from './componentes/ModalTratamento';
import { ModalAlertaClinico } from './componentes/ModalAlertaClinico';
import { ModalCadastroPaciente } from './componentes/ModalCadastroPaciente';
import { CarteiraVacinacao } from './componentes/CarteiraVacinacao';
import { ModalReceita, Receituario } from './componentes/Receituario';
import { ModalPedidoExame, PedidosExame } from './componentes/PedidosExame';
import { ModalReceitaOuExame } from './componentes/ModalReceitaOuExame';
import {
  ABA_DA_EMISSAO,
  tratamentosAtivos,
  type OrigemClinica,
  type SessaoDeOrigem,
  type SessaoRegistrada,
  type TipoDeEmissao,
} from '../utils/vinculoClinico';
import { LinhaDoTempo, ModalRegistroCompleto } from './componentes/LinhaDoTempoClinica';
import { AvaliacoesDoPaciente } from './componentes/AvaliacoesDoPaciente';
import { TratamentosDoPaciente, type DocumentosPorTratamento } from './componentes/TratamentosDoPaciente';
import { SessoesDoPaciente } from './componentes/SessoesDoPaciente';
import { ModalCadastrarSessoes } from './componentes/ModalCadastrarSessoes';
import type { AcoesDaSessao } from './componentes/ListaDeSessoes';

type Aba =
  | 'linha-do-tempo'
  | 'avaliacoes'
  | 'tratamentos'
  | 'sessoes'
  | 'vacinas'
  | 'medicacao'
  | 'exames'
  | 'documentos';

const ABAS: Aba[] = [
  'linha-do-tempo',
  'avaliacoes',
  'tratamentos',
  'sessoes',
  'vacinas',
  'medicacao',
  'exames',
  'documentos',
];

/**
 * Endereços antigos continuam abrindo a aba equivalente: as evoluções viraram Sessões, a aba
 * de receitas virou Medicação e a de gráficos saiu do prontuário.
 */
const ABAS_RENOMEADAS: Record<string, Aba> = {
  evolucao: 'sessoes',
  evolucoes: 'sessoes',
  graficos: 'linha-do-tempo',
  receitas: 'medicacao',
};

const TIPOS_DE_DOCUMENTO = ['Contrato', 'Exame', 'Laudo', 'Outro'];

/**
 * HU-011: prontuário do animal. O fluxo clínico segue animal → avaliações → tratamentos
 * originados das avaliações → sessões registradas de cada tratamento. A linha do tempo reúne
 * os principais eventos; receitas e pedidos de exame nascem de uma avaliação ou de um
 * tratamento (direto nele ou no registro de uma das sessões dele).
 */
export function Prontuario() {
  const { pacienteId = '' } = useParams();
  const [parametros, setParametros] = useSearchParams();
  const { podeVerObservacoesInternas, temPerfil, ehTutor, ehVeterinario, usuario } = useAuth();
  const { confirmar, perguntar } = useConfirmacao();

  const podeRegistrar = temPerfil('Administrador', 'Veterinario');
  // Cadastrar, inativar e reativar sessões é também tarefa da recepção (Apoio).
  const podeGerirSessoes = temPerfil('Administrador', 'Veterinario', 'Apoio');
  // RN-014: a ficha cadastral do paciente é mantida pela administração; o veterinário
  // registra o conteúdo clínico do prontuário, não o cadastro.
  const podeEditarCadastro = temPerfil('Administrador', 'Apoio');
  const podeAnexarDocumento = temPerfil('Administrador', 'Veterinario', 'Apoio');

  // O tutor não alcança a lista geral de pacientes: ele volta para os próprios pets.
  const voltarPara = ehTutor ? '/meus-pets' : '/pacientes';
  const rotuloVoltar = ehTutor ? 'Voltar para meus pets' : 'Voltar para pacientes';

  // A aba vem na URL (?aba=vacinas) para que atalhos de outras telas abram direto nela.
  const abaDaUrl = parametros.get('aba') ?? '';
  const aba: Aba = ABAS.includes(abaDaUrl as Aba) ? (abaDaUrl as Aba) : (ABAS_RENOMEADAS[abaDaUrl] ?? 'linha-do-tempo');

  function mudarAba(nova: Aba) {
    if (nova === 'linha-do-tempo') parametros.delete('aba');
    else parametros.set('aba', nova);
    setParametros(parametros, { replace: true });
  }

  // Endereços antigos do atalho "Emitir receita desta sessão" chegam com a origem na URL.
  const evolucaoDaUrl = parametros.get('evolucao');
  const avaliacaoDaUrl = parametros.get('avaliacao');
  const origemDaUrl: OrigemClinica | null = evolucaoDaUrl
    ? { atendimentoId: evolucaoDaUrl }
    : avaliacaoDaUrl
      ? { avaliacaoId: avaliacaoDaUrl }
      : null;

  // Quem chega de outra tela depois de emitir uma receita ou um pedido de exame traz a confirmação.
  const location = useLocation();
  const navigate = useNavigate();
  const [aviso, setAviso] = useState(() => (location.state as { aviso?: string } | null)?.aviso ?? '');

  useEffect(() => {
    // A confirmação é mostrada uma vez: recarregar a página não a repete.
    if ((location.state as { aviso?: string } | null)?.aviso) {
      navigate({ pathname: location.pathname, search: location.search }, { replace: true, state: null });
    }
  }, [location, navigate]);
  // "Abrir no prontuário", na tela de Sessões, chega com o tratamento na URL: ele abre expandido e à vista.
  const tratamentoDaUrl = parametros.get('tratamento');
  const [foco, setFoco] = useState<{ avaliacao?: string; tratamento?: string; receita?: string; pedido?: string }>(() =>
    tratamentoDaUrl ? { tratamento: tratamentoDaUrl } : {},
  );
  // Filtro de origem das abas Medicação e Exames, vindo dos atalhos de uma avaliação ou de um tratamento.
  const [filtroDosDocumentos, setFiltroDosDocumentos] = useState({ medicacao: '', exames: '' });

  const [modalAvaliacao, setModalAvaliacao] = useState(false);
  const [avaliacaoEmCorrecao, setAvaliacaoEmCorrecao] = useState<Avaliacao | null>(null);
  const [origemDoTratamento, setOrigemDoTratamento] = useState<Avaliacao | null>(null);
  const [tratamentoEmEdicao, setTratamentoEmEdicao] = useState<Tratamento | null>(null);
  const [evolucaoAberta, setEvolucaoAberta] = useState<{ atendimento: Atendimento; corrigir: boolean } | null>(null);
  // Sessão cadastrada cujo atendimento está sendo registrado.
  const [sessaoParaRegistro, setSessaoParaRegistro] = useState<SessaoParaRegistro | null>(null);
  // Tratamento que está recebendo sessões novas.
  const [cadastroDeSessoes, setCadastroDeSessoes] = useState<Tratamento | null>(null);
  const [receita, setReceita] = useState<{ aberta: boolean; origem: OrigemClinica | null }>({ aberta: false, origem: null });
  // Pedido novo (com a origem já escolhida, quando parte de uma avaliação) ou em edição.
  const [pedidoExame, setPedidoExame] = useState<{ aberto: boolean; origem?: OrigemClinica; pedido?: PedidoExame }>({
    aberto: false,
  });
  // Receita ou pedido de exame de uma sessão aberta pela linha do tempo.
  const [emissaoDaSessao, setEmissaoDaSessao] = useState<SessaoDeOrigem | null>(null);
  const [registroAberto, setRegistroAberto] = useState<ItemLinhaTempo | null>(null);
  const [modalAlerta, setModalAlerta] = useState(false);
  const [petEmEdicao, setPetEmEdicao] = useState<Pet | null>(null);
  const [tipoDocumento, setTipoDocumento] = useState('Exame');
  const [enviandoDocumento, setEnviandoDocumento] = useState(false);
  const [nomeClinica, setNomeClinica] = useState('Clínica VetSPA');

  const buscar = useCallback(async () => {
    const { data } = await api.get<ProntuarioDTO>(`/api/prontuarios/paciente/${pacienteId}`);
    return data;
  }, [pacienteId]);

  const {
    dados: prontuario,
    carregando,
    erro,
    setErro,
    recarregar,
  } = useCarregamento(buscar, 'Não foi possível carregar o prontuário.');

  const prontuarioCarregado = !!prontuario;
  useEffect(() => {
    if (!prontuarioCarregado || !tratamentoDaUrl) return;

    const espera = window.setTimeout(
      () => document.getElementById(`tratamento-${tratamentoDaUrl}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      80,
    );
    return () => window.clearTimeout(espera);
  }, [prontuarioCarregado, tratamentoDaUrl]);

  // RN-004: o prontuário é a soma de muitos registros; qualquer um deles que mude na
  // clínica precisa aparecer aqui, inclusive para o tutor que está com a tela aberta.
  useAtualizacao(
    [
      'prontuarios',
      'avaliacoes',
      'atendimentos',
      'prescricoes',
      'pedidosexame',
      'vacinas',
      'alergias',
      'documentos',
      'midias',
      'tratamentos',
      'observacoesinternas',
      'pets',
      'sessoes',
      'tutores',
      'usuarios',
      'clinica',
    ],
    recarregar,
  );

  // O nome da clínica compõe o cabeçalho dos documentos impressos.
  useEffect(() => {
    api
      .get<{ nome: string }>('/api/clinica')
      .then(({ data }) => setNomeClinica(data.nome))
      .catch(() => undefined);
  }, []);

  /** Leva à aba e rola até o registro, que ganha destaque. */
  function irPara(destino: Aba, ancora?: string) {
    setRegistroAberto(null);
    mudarAba(destino);

    if (ancora) {
      window.setTimeout(() => document.getElementById(ancora)?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 80);
    }
  }

  function verAvaliacao(id: string) {
    setFoco({ avaliacao: id });
    irPara('avaliacoes', `avaliacao-${id}`);
  }

  function verTratamento(id: string) {
    setFoco({ tratamento: id });
    irPara('tratamentos', `tratamento-${id}`);
  }

  /** Receitas ou pedidos de exame de uma avaliação ou de um tratamento ("avaliacao:<id>", "tratamento:<id>"). */
  function verDocumentos(destino: 'medicacao' | 'exames', filtro: string) {
    setFiltroDosDocumentos((atual) => ({ ...atual, [destino]: filtro }));
    irPara(destino);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /** Abre a receita ou o pedido da linha do tempo, expandido, sem filtro que o esconda. */
  function verDocumento(destino: 'medicacao' | 'exames', id: string) {
    setFoco(destino === 'medicacao' ? { receita: id } : { pedido: id });
    setFiltroDosDocumentos((atual) => ({ ...atual, [destino]: '' }));
    irPara(destino, destino === 'medicacao' ? `receita-${id}` : `pedido-${id}`);
  }

  /** Depois de emitir, a aba do documento abre sem filtro, com o novo à vista. */
  function mostrarEmitido(destino: 'medicacao' | 'exames') {
    setFiltroDosDocumentos((atual) => ({ ...atual, [destino]: '' }));
    mudarAba(destino);
  }

  function concluido(mensagem: string) {
    setAviso(mensagem);
    recarregar();
  }

  function fecharReceita() {
    setReceita({ aberta: false, origem: null });

    if (origemDaUrl) {
      parametros.delete('evolucao');
      parametros.delete('avaliacao');
      setParametros(parametros, { replace: true });
    }
  }

  /** Evolução completa, com fotos e correção, para Administrador e Veterinário. */
  async function abrirEvolucao(id: string, corrigir = false) {
    setErro('');

    try {
      const { data } = await api.get<Atendimento>(`/api/atendimentos/${id}`);
      setRegistroAberto(null);
      setEvolucaoAberta({ atendimento: data, corrigir });
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível abrir a sessão.'));
    }
  }

  async function abrirCadastro() {
    setErro('');

    try {
      const { data } = await api.get<Pet>(`/api/pets/${pacienteId}`);
      setPetEmEdicao(data);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível abrir o cadastro do paciente.'));
    }
  }

  /** HU-012: anexo de contratos, exames externos e laudos. */
  async function enviarDocumento(arquivo: File) {
    setErro('');
    setEnviandoDocumento(true);

    const corpo = new FormData();
    corpo.append('pacienteId', pacienteId);
    corpo.append('tipoDocumento', tipoDocumento);
    corpo.append('arquivo', arquivo);

    try {
      await api.post('/api/documentos', corpo);
      concluido('Documento anexado com sucesso.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível anexar o documento.'));
    } finally {
      setEnviandoDocumento(false);
    }
  }

  /** HU-012, CA-2: o download passa pela API, que valida a autorização e registra o acesso. */
  async function baixarDocumento(documento: Documento) {
    try {
      const { data } = await api.get<Blob>(`/api/documentos/${documento.id}/download`, { responseType: 'blob' });

      const url = URL.createObjectURL(data);
      const link = document.createElement('a');
      link.href = url;
      link.download = documento.nomeArquivo;
      document.body.appendChild(link);
      link.click();
      link.remove();

      // Revogar na hora cancelaria o download em alguns navegadores.
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível baixar o documento.'));
    }
  }

  if (carregando) {
    return <Carregando texto="Carregando prontuário..." />;
  }

  if (!prontuario) {
    return (
      <>
        <Link to={voltarPara} className="mb-4 inline-flex items-center gap-1 text-sm text-brand hover:underline">
          <ArrowLeft size={15} />
          Voltar
        </Link>
        <Alerta tipo="erro">{erro || 'Prontuário não encontrado.'}</Alerta>
      </>
    );
  }

  // Registros novos só com o paciente vivo; a correção do que já existe continua possível.
  const podeAgir = podeRegistrar && !prontuario.dataObito;
  const podeCadastrarSessoes = podeGerirSessoes && !prontuario.dataObito;

  const dosesPendentes = prontuario.vacinas.filter(
    (v) => v.situacaoDose === 'Vencida' || v.situacaoDose === 'A vencer',
  ).length;

  // Doses vencidas entram nos alertas clínicos, ao lado das alergias e comorbidades.
  const dosesVencidas = prontuario.vacinas.filter((v) => v.situacaoDose === 'Vencida');

  // As sessões registradas, da mais recente à mais antiga: receitas e pedidos também nascem no registro delas.
  const sessoesRegistradas: SessaoRegistrada[] = prontuario.historico
    .filter((item) => item.categoria === 'Sessão')
    .map((item) => ({
      id: item.id,
      data: item.data,
      numeroSessao: item.numeroSessao ?? 0,
      tratamentoId: item.tratamentoId ?? '',
      nomeTratamento: prontuario.tratamentos.find((t) => t.id === item.tratamentoId)?.nome ?? 'Tratamento',
      nomeVeterinario: item.autor,
    }));

  // Receitas e pedidos de cada tratamento, para os atalhos do cartão do tratamento.
  const documentosPorTratamento: DocumentosPorTratamento = {};

  for (const receita of prontuario.prescricoes) {
    if (receita.tratamentoId) (documentosPorTratamento[receita.tratamentoId] ??= { receitas: 0, pedidos: 0 }).receitas++;
  }

  for (const pedido of prontuario.pedidosExame) {
    if (pedido.tratamentoId) (documentosPorTratamento[pedido.tratamentoId] ??= { receitas: 0, pedidos: 0 }).pedidos++;
  }

  const abas: { valor: Aba; rotulo: string; contador?: number; alerta?: boolean }[] = [
    { valor: 'linha-do-tempo', rotulo: 'Linha do tempo', contador: prontuario.historico.length },
    { valor: 'avaliacoes', rotulo: 'Avaliações', contador: prontuario.avaliacoes.length },
    { valor: 'tratamentos', rotulo: 'Tratamentos', contador: prontuario.tratamentos.length },
    { valor: 'sessoes', rotulo: 'Sessões', contador: prontuario.sessoes.filter((s) => !s.inativa).length },
    { valor: 'vacinas', rotulo: 'Vacinação', contador: dosesPendentes, alerta: dosesPendentes > 0 },
    { valor: 'medicacao', rotulo: 'Medicação', contador: prontuario.prescricoes.length },
    { valor: 'exames', rotulo: 'Exames', contador: prontuario.pedidosExame.length },
    { valor: 'documentos', rotulo: 'Documentos', contador: prontuario.documentos.length },
  ];

  function emitirReceita(origem: OrigemClinica) {
    setRegistroAberto(null);
    setEvolucaoAberta(null);
    setSessaoParaRegistro(null);
    setReceita({ aberta: true, origem });
  }

  /** Receita ou pedido de exame a partir de uma sessão registrada, aberta pela linha do tempo. */
  function emitirDaSessao(atendimentoId: string) {
    const sessao = sessoesRegistradas.find((s) => s.id === atendimentoId);
    if (!sessao) return;

    setRegistroAberto(null);
    setEmissaoDaSessao({ ...sessao, pacienteId, nomePaciente: prontuario!.nomePaciente });
  }

  /** Depois de emitir pela sessão, o usuário vai para a aba do documento escolhido. */
  function concluirEmissao(tipo: TipoDeEmissao, mensagem: string) {
    setEmissaoDaSessao(null);
    setEvolucaoAberta(null);
    setSessaoParaRegistro(null);
    concluido(mensagem);
    mostrarEmitido(ABA_DA_EMISSAO[tipo]);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /** Fecha o registro da sessão; se algo foi salvo, o prontuário é relido. */
  function fecharEvolucao(salvou: boolean) {
    setEvolucaoAberta(null);
    setSessaoParaRegistro(null);
    if (salvou) recarregar();
  }

  /** Retira a sessão do plano, com um motivo opcional que fica nas observações e na auditoria. */
  async function inativarSessao(sessao: Sessao) {
    const resposta = await perguntar({
      titulo: 'Inativar sessão',
      mensagem: (
        <>
          Inativar a sessão de <strong>{formatarData(sessao.dataHora)}</strong>? Ela sai da agenda e das contagens do
          tratamento, mas fica no histórico e pode ser reativada. Se ainda ia acontecer, o tutor é avisado.
        </>
      ),
      rotuloConfirmar: 'Inativar',
      rotuloCancelar: 'Voltar',
      perigo: true,
      campoTexto: { rotulo: 'Motivo', placeholder: 'Ex.: cadastrada em dobro' },
    });

    if (!resposta.confirmado) return;

    try {
      await api.patch(`/api/sessoes/${sessao.id}/inativar`, { motivo: resposta.texto || null });
      concluido('Sessão inativada. Ela saiu da agenda e pode ser reativada.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível inativar a sessão.'));
    }
  }

  async function reativarSessao(sessao: Sessao) {
    if (
      !(await confirmar({
        titulo: 'Reativar sessão',
        mensagem: `Reativar a sessão de ${formatarData(sessao.dataHora)}? Ela volta ao plano do tratamento, se o horário ainda estiver livre.`,
        rotuloConfirmar: 'Reativar',
      }))
    ) {
      return;
    }

    try {
      await api.patch(`/api/sessoes/${sessao.id}/reativar`);
      concluido('Sessão reativada.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível reativar a sessão.'));
    }
  }

  /** O registro do atendimento: completo (com fotos e correção) para a equipe clínica; o resumo para os demais. */
  function abrirRegistroDaSessao(sessao: Sessao) {
    if (!sessao.atendimentoId) return;

    if (podeRegistrar) {
      abrirEvolucao(sessao.atendimentoId);
      return;
    }

    const item = prontuario!.historico.find((h) => h.categoria === 'Sessão' && h.id === sessao.atendimentoId);
    if (item) setRegistroAberto(item);
  }

  const acoesDaSessao: AcoesDaSessao = {
    aoRegistrar: podeAgir
      ? (sessao) =>
          setSessaoParaRegistro({
            sessaoId: sessao.id,
            tratamentoId: sessao.tratamentoId,
            nomePaciente: prontuario.nomePaciente,
            nomeTutor: prontuario.nomeTutor,
            dataHora: sessao.dataHora,
            dataHoraFim: sessao.dataHoraFim,
          })
      : undefined,
    aoAbrirRegistro: abrirRegistroDaSessao,
    aoInativar: podeCadastrarSessoes ? inativarSessao : undefined,
    aoReativar: podeCadastrarSessoes ? reativarSessao : undefined,
    // O veterinário mexe só nas sessões da própria agenda; a API confere de novo.
    podeAlterar: (sessao) => !ehVeterinario || sessao.veterinarioId === usuario?.veterinarioId,
  };

  function criarTratamento(avaliacao: Avaliacao) {
    setRegistroAberto(null);
    setOrigemDoTratamento(avaliacao);
  }

  function pedirExames(origem?: OrigemClinica) {
    setRegistroAberto(null);
    setPedidoExame({ aberto: true, origem });
  }

  function corrigirAvaliacao(avaliacao: Avaliacao) {
    setRegistroAberto(null);
    setAvaliacaoEmCorrecao(avaliacao);
    setModalAvaliacao(true);
  }

  /** Atalhos do registro completo, conforme a categoria do evento e o perfil. */
  function acoesDoRegistro(item: ItemLinhaTempo): ReactNode {
    const botao = (rotulo: string, icone: ReactNode, aoClicar: () => void, primario = false) => (
      <button
        key={rotulo}
        type="button"
        className={primario ? 'vc-botao-primario' : 'vc-botao-secundario'}
        onClick={aoClicar}
      >
        {icone}
        {rotulo}
      </button>
    );

    switch (item.categoria) {
      case 'Avaliação': {
        const avaliacao = prontuario!.avaliacoes.find((a) => a.id === item.id);
        return [
          botao('Ver em Avaliações', <ClipboardList size={15} />, () => verAvaliacao(item.id)),
          avaliacao && podeRegistrar && botao('Corrigir', <Pencil size={15} />, () => corrigirAvaliacao(avaliacao)),
          avaliacao && podeAgir && botao('Emitir receita', <Pill size={15} />, () => emitirReceita({ avaliacaoId: item.id })),
          avaliacao && podeAgir && botao('Pedir exames', <FlaskConical size={15} />, () => pedirExames({ avaliacaoId: item.id })),
          avaliacao &&
            podeAgir &&
            botao('Criar tratamento', <Stethoscope size={15} />, () => criarTratamento(avaliacao), true),
        ];
      }
      case 'Tratamento':
        return botao('Abrir tratamento', <Stethoscope size={15} />, () => verTratamento(item.id), true);
      case 'Sessão':
        return [
          item.tratamentoId &&
            botao('Ver tratamento', <Stethoscope size={15} />, () => verTratamento(item.tratamentoId!)),
          podeAgir &&
            botao('Receita ou pedido de exame', <ClipboardPlus size={15} />, () => emitirDaSessao(item.id)),
          podeRegistrar && botao('Abrir sessão', <Activity size={15} />, () => abrirEvolucao(item.id), true),
        ];
      case 'Receita':
        return [
          item.tratamentoId &&
            botao('Ver tratamento', <Stethoscope size={15} />, () => verTratamento(item.tratamentoId!)),
          botao('Ver medicação', <Pill size={15} />, () => verDocumento('medicacao', item.id), true),
        ];
      case 'Exame':
        return [
          item.tratamentoId &&
            botao('Ver tratamento', <Stethoscope size={15} />, () => verTratamento(item.tratamentoId!)),
          botao('Ver pedidos de exame', <FlaskConical size={15} />, () => verDocumento('exames', item.id), true),
        ];
      case 'Vacinação':
        return botao('Abrir carteira de vacinação', <Syringe size={15} />, () => irPara('vacinas'), true);
      default:
        return null;
    }
  }

  return (
    <>
      <Link to={voltarPara} className="mb-4 inline-flex items-center gap-1 text-sm text-brand hover:underline">
        <ArrowLeft size={15} />
        {rotuloVoltar}
      </Link>

      <CabecalhoPagina
        titulo={prontuario.nomePaciente}
        descricao={[
          prontuario.especie,
          prontuario.raca,
          prontuario.idadeDescritiva,
          prontuario.sexo,
          prontuario.castrado ? 'Castrado' : null,
          prontuario.microchip ? `Chip ${prontuario.microchip}` : null,
          `Tutor: ${prontuario.nomeTutor}`,
          `Veterinário: ${prontuario.nomeVeterinarioResponsavel || 'sem responsável'}`,
        ]
          .filter(Boolean)
          .join(' · ')}
        acoes={
          <>
            <button
              type="button"
              className="vc-botao-secundario"
              onClick={() => imprimirProntuario(prontuario, nomeClinica)}
            >
              <Printer size={16} />
              Imprimir
            </button>

            {podeEditarCadastro && (
              <button type="button" className="vc-botao-secundario" onClick={abrirCadastro}>
                <Pencil size={16} />
                Editar cadastro
              </button>
            )}

            {podeRegistrar && (
              <button type="button" className="vc-botao-secundario" onClick={() => setModalAlerta(true)}>
                <ShieldAlert size={16} />
                Alerta clínico
              </button>
            )}

            {podeAgir && (
              <button
                type="button"
                className="vc-botao-primario"
                onClick={() => {
                  setAvaliacaoEmCorrecao(null);
                  setModalAvaliacao(true);
                }}
              >
                <ClipboardList size={16} />
                Registrar avaliação
              </button>
            )}
          </>
        }
      />

      {erro && (
        <Alerta flutuante tipo="erro" aoFechar={() => setErro('')}>
          {erro}
        </Alerta>
      )}
      {aviso && (
        <Alerta flutuante tipo="sucesso" aoFechar={() => setAviso('')}>
          {aviso}
        </Alerta>
      )}

      {/* Todo paciente precisa de um responsável; só o cadastrado pelo tutor no aplicativo chega sem. */}
      {!prontuario.veterinarioResponsavelId && !prontuario.dataObito && podeEditarCadastro && (
        <div className="mb-4">
          <Alerta tipo="aviso">
            Este paciente foi cadastrado pelo tutor e ainda não tem veterinário responsável.{' '}
            <button type="button" className="font-semibold underline" onClick={abrirCadastro}>
              Defina o responsável em Editar cadastro
            </button>
            .
          </Alerta>
        </div>
      )}

      {prontuario.dataObito && (
        <div className="mb-4">
          <Alerta tipo="aviso">
            Paciente registrado como falecido em {formatarDia(prontuario.dataObito)}. O prontuário
            permanece disponível apenas para consulta.
          </Alerta>
        </div>
      )}

      {/* Alergias, comorbidades e vacinação em atraso precisam ser lidas antes de qualquer conduta. */}
      {(prontuario.alertasClinicos.length > 0 || dosesVencidas.length > 0) && (
        <div className="mb-6">
          <AlertasClinicos
            alertas={prontuario.alertasClinicos}
            dosesVencidas={dosesVencidas}
            aoVerVacinas={aba === 'vacinas' ? undefined : () => mudarAba('vacinas')}
          />
        </div>
      )}

      {/*
        HU-011, CA-2 e RN-003: quem não é Administrador nem Veterinário recebe a versão
        filtrada. O texto distingue o Tutor do Apoio administrativo, que também não
        enxerga as anotações internas.
      */}
      {!prontuario.exibeObservacoesInternas && (
        <div className="mb-4">
          <Alerta tipo="info">
            {ehTutor
              ? 'Você está vendo as informações autorizadas do prontuário do seu pet. As anotações internas da equipe clínica não fazem parte desta visão.'
              : 'Seu perfil acessa o prontuário sem as observações internas, que são restritas a Administrador e Veterinário.'}
          </Alerta>
        </div>
      )}

      <div className="mb-6 flex flex-wrap gap-1 border-b border-slate-200" role="tablist">
        {abas.map((item) => (
          <button
            key={item.valor}
            type="button"
            role="tab"
            aria-selected={aba === item.valor}
            onClick={() => mudarAba(item.valor)}
            className={`-mb-px border-b-2 px-4 py-2.5 text-sm font-medium transition ${
              aba === item.valor
                ? 'border-brand text-brand'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}
          >
            {item.rotulo}
            {item.contador !== undefined && item.contador > 0 && (
              <span
                className={`ml-1.5 rounded-full px-1.5 py-0.5 text-xs ${
                  item.alerta ? 'bg-alerta-claro font-semibold text-amber-800' : 'bg-slate-100 text-slate-600'
                }`}
              >
                {item.contador}
              </span>
            )}
          </button>
        ))}
      </div>

      {aba === 'linha-do-tempo' && <LinhaDoTempo itens={prontuario.historico} aoSelecionar={setRegistroAberto} />}

      {aba === 'avaliacoes' && (
        <AvaliacoesDoPaciente
          avaliacoes={prontuario.avaliacoes}
          emFoco={foco.avaliacao}
          aoRegistrar={
            podeAgir
              ? () => {
                  setAvaliacaoEmCorrecao(null);
                  setModalAvaliacao(true);
                }
              : undefined
          }
          aoCriarTratamento={podeAgir ? criarTratamento : undefined}
          aoEmitirReceita={podeAgir ? (avaliacao) => emitirReceita({ avaliacaoId: avaliacao.id }) : undefined}
          aoPedirExames={podeAgir ? (avaliacao) => pedirExames({ avaliacaoId: avaliacao.id }) : undefined}
          aoCorrigir={podeRegistrar ? corrigirAvaliacao : undefined}
          aoVerTratamento={verTratamento}
          aoVerDocumentos={verDocumentos}
        />
      )}

      {aba === 'tratamentos' && (
        <TratamentosDoPaciente
          pacienteId={pacienteId}
          tratamentos={prontuario.tratamentos}
          sessoes={prontuario.sessoes}
          emFoco={foco.tratamento}
          podeGerenciar={podeAgir}
          exibeLinkDaTelaDeSessoes={podeRegistrar}
          aoEditar={setTratamentoEmEdicao}
          aoCadastrarSessoes={podeCadastrarSessoes ? setCadastroDeSessoes : undefined}
          acoesDaSessao={acoesDaSessao}
          aoEmitirReceita={podeAgir ? (tratamento) => emitirReceita({ tratamentoId: tratamento.id }) : undefined}
          aoPedirExames={podeAgir ? (tratamento) => pedirExames({ tratamentoId: tratamento.id }) : undefined}
          documentosPorTratamento={documentosPorTratamento}
          aoVerDocumentos={verDocumentos}
          aoVerAvaliacao={verAvaliacao}
          aoAtualizar={concluido}
          aoFalhar={setErro}
        />
      )}

      {aba === 'sessoes' && (
        <SessoesDoPaciente
          pacienteId={pacienteId}
          sessoes={prontuario.sessoes}
          tratamentos={prontuario.tratamentos}
          acoesDaSessao={acoesDaSessao}
          exibeLinkDaTela={podeRegistrar}
        />
      )}

      {aba === 'vacinas' && (
        <CarteiraVacinacao
          pacienteId={pacienteId}
          vacinas={prontuario.vacinas}
          somenteLeitura={!!prontuario.dataObito}
          aoAtualizar={(mensagem) => {
            if (mensagem) setAviso(mensagem);
            recarregar();
          }}
        />
      )}

      {aba === 'medicacao' && (
        <Receituario
          // Um atalho com outro filtro remonta a lista já filtrada.
          key={`receitas-${filtroDosDocumentos.medicacao}`}
          prescricoes={prontuario.prescricoes}
          nomeClinica={nomeClinica}
          aoEmitir={podeAgir ? () => setReceita({ aberta: true, origem: null }) : undefined}
          aoAtualizar={(mensagem) => {
            if (mensagem) setAviso(mensagem);
            recarregar();
          }}
          aoVerAvaliacao={verAvaliacao}
          aoVerTratamento={verTratamento}
          filtroInicial={filtroDosDocumentos.medicacao}
          emFoco={foco.receita}
        />
      )}

      {aba === 'exames' && (
        <PedidosExame
          key={`pedidos-${filtroDosDocumentos.exames}`}
          pedidos={prontuario.pedidosExame}
          nomeClinica={nomeClinica}
          temOrigens={prontuario.avaliacoes.length > 0 || tratamentosAtivos(prontuario.tratamentos).length > 0}
          aoPedir={podeAgir ? () => pedirExames() : undefined}
          aoEditar={podeRegistrar ? (pedido) => setPedidoExame({ aberto: true, pedido }) : undefined}
          aoAtualizar={(mensagem) => {
            if (mensagem) setAviso(mensagem);
            recarregar();
          }}
          aoVerAvaliacao={verAvaliacao}
          aoVerTratamento={verTratamento}
          filtroInicial={filtroDosDocumentos.exames}
          emFoco={foco.pedido}
        />
      )}

      {aba === 'documentos' && (
        <Card>
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
            <h2 className="font-semibold text-slate-900">Documentos clínicos</h2>

            {podeAnexarDocumento && (
              <div className="flex items-center gap-2">
                <select
                  className="vc-campo w-auto py-2"
                  value={tipoDocumento}
                  onChange={(e) => setTipoDocumento(e.target.value)}
                  aria-label="Tipo do documento"
                >
                  {TIPOS_DE_DOCUMENTO.map((tipo) => (
                    <option key={tipo}>{tipo}</option>
                  ))}
                </select>

                <label className="vc-botao-secundario cursor-pointer">
                  {enviandoDocumento ? <Loader2 className="animate-spin" size={16} /> : <Paperclip size={16} />}
                  Anexar
                  <input
                    type="file"
                    className="sr-only"
                    accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
                    disabled={enviandoDocumento}
                    onChange={(e) => {
                      const arquivo = e.target.files?.[0];

                      if (arquivo) enviarDocumento(arquivo);
                      e.target.value = '';
                    }}
                  />
                </label>
              </div>
            )}
          </div>

          {prontuario.documentos.length === 0 ? (
            <SemDados
              icone={<FileText size={40} />}
              titulo="Nenhum documento anexado"
              descricao="Contratos, exames externos e laudos em PDF ficam centralizados aqui. Formatos aceitos: PDF, JPG, PNG, DOC e DOCX (até 10 MB)."
            />
          ) : (
            <ul className="divide-y divide-slate-100">
              {prontuario.documentos.map((documento) => (
                <li key={documento.id} className="flex flex-wrap items-center gap-4 px-5 py-4">
                  <div className="rounded-xl bg-brand-50 p-2.5 text-brand">
                    <FileText size={20} />
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium text-slate-900">{documento.nomeArquivo}</p>
                    <p className="text-xs text-slate-500">
                      {documento.tipoDocumento} · {formatarTamanho(documento.tamanhoBytes)} ·{' '}
                      {formatarData(documento.dataUpload)}
                      {documento.enviadoPor && ` · ${documento.enviadoPor}`}
                    </p>
                  </div>

                  <button type="button" onClick={() => baixarDocumento(documento)} className="vc-botao-secundario">
                    <Download size={15} />
                    Baixar
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}

      <ModalRegistroCompleto
        item={registroAberto}
        exibeObservacoes={podeVerObservacoesInternas}
        acoes={registroAberto ? acoesDoRegistro(registroAberto) : undefined}
        aoFechar={() => setRegistroAberto(null)}
      />

      <ModalAvaliacao
        aberto={modalAvaliacao}
        pacienteId={pacienteId}
        veterinarioResponsavelId={prontuario.veterinarioResponsavelId}
        nomeVeterinarioResponsavel={prontuario.nomeVeterinarioResponsavel}
        avaliacao={avaliacaoEmCorrecao}
        aoFechar={() => {
          setModalAvaliacao(false);
          setAvaliacaoEmCorrecao(null);
        }}
        aoSalvar={() => {
          setModalAvaliacao(false);
          concluido(
            avaliacaoEmCorrecao
              ? 'Avaliação corrigida. A versão anterior foi preservada no histórico.'
              : 'Avaliação registrada. A partir dela você pode criar tratamentos, emitir receitas e pedir exames.',
          );
          setAvaliacaoEmCorrecao(null);
          if (!avaliacaoEmCorrecao) mudarAba('avaliacoes');
        }}
      />

      <ModalTratamento
        aberto={!!origemDoTratamento || !!tratamentoEmEdicao}
        avaliacao={origemDoTratamento}
        tratamento={tratamentoEmEdicao}
        veterinarioResponsavelId={prontuario.veterinarioResponsavelId}
        nomeVeterinarioResponsavel={prontuario.nomeVeterinarioResponsavel}
        aoFechar={() => {
          setOrigemDoTratamento(null);
          setTratamentoEmEdicao(null);
        }}
        aoSalvar={(mensagem) => {
          const novo = !!origemDoTratamento;
          setOrigemDoTratamento(null);
          setTratamentoEmEdicao(null);
          concluido(mensagem);
          if (novo) mudarAba('tratamentos');
        }}
      />

      <ModalRegistrarAtendimento
        key={sessaoParaRegistro?.sessaoId ?? evolucaoAberta?.atendimento.id ?? 'nenhuma'}
        atendimento={evolucaoAberta?.atendimento}
        sessao={sessaoParaRegistro}
        corrigir={evolucaoAberta?.corrigir}
        aoEmitirDocumento={concluirEmissao}
        permiteEmissao={podeAgir}
        aoFechar={() => fecharEvolucao(false)}
        aoSalvar={() => fecharEvolucao(true)}
      />

      <ModalCadastrarSessoes
        tratamento={cadastroDeSessoes}
        descricao={`${prontuario.nomePaciente} · ${prontuario.nomeTutor}`}
        cadastradas={prontuario.sessoes.filter(
          (s) => s.tratamentoId === cadastroDeSessoes?.id && !s.inativa && s.status !== 'Cancelada',
        )}
        aoFechar={() => setCadastroDeSessoes(null)}
        aoSalvar={(mensagem) => {
          setCadastroDeSessoes(null);
          concluido(mensagem);
        }}
      />

      <ModalReceita
        aberto={receita.aberta || (!!origemDaUrl && podeAgir)}
        pacienteId={pacienteId}
        avaliacoes={prontuario.avaliacoes}
        sessoes={sessoesRegistradas}
        tratamentos={prontuario.tratamentos}
        origem={receita.aberta ? receita.origem : origemDaUrl}
        aoFechar={fecharReceita}
        aoSalvar={(mensagem) => {
          fecharReceita();
          concluido(mensagem);
          mostrarEmitido('medicacao');
        }}
      />

      <ModalPedidoExame
        aberto={pedidoExame.aberto}
        avaliacoes={prontuario.avaliacoes}
        sessoes={sessoesRegistradas}
        tratamentos={prontuario.tratamentos}
        origem={pedidoExame.origem}
        pedido={pedidoExame.pedido}
        aoFechar={() => setPedidoExame({ aberto: false })}
        aoSalvar={(mensagem) => {
          setPedidoExame({ aberto: false });
          concluido(mensagem);
          mostrarEmitido('exames');
        }}
      />

      <ModalReceitaOuExame
        sessao={emissaoDaSessao}
        aoFechar={() => setEmissaoDaSessao(null)}
        aoSalvar={concluirEmissao}
      />

      <ModalAlertaClinico
        aberto={modalAlerta}
        pacienteId={pacienteId}
        alertas={prontuario.alertasClinicos}
        aoFechar={() => setModalAlerta(false)}
        aoSalvar={(mensagem) => {
          setModalAlerta(false);
          concluido(mensagem);
        }}
      />

      <ModalCadastroPaciente
        aberto={!!petEmEdicao}
        pet={petEmEdicao}
        aoFechar={() => setPetEmEdicao(null)}
        aoSalvar={(mensagem) => {
          setPetEmEdicao(null);
          concluido(mensagem);
        }}
      />
    </>
  );
}
