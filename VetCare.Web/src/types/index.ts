/** Contratos compartilhados com a VetCare.API. */

export type Perfil = 'Administrador' | 'Veterinario' | 'Tutor' | 'Apoio';

/** Resposta padrão das listagens paginadas da API. */
export interface PaginaDe<T> {
  itens: T[];
  pagina: number;
  tamanho: number;
  total: number;
  totalDePaginas: number;
  temAnterior: boolean;
  temProxima: boolean;
}

export type StatusSessao = 'Aguardando confirmação' | 'Confirmada' | 'Cancelada' | 'Concluída';

export type VisaoAgenda = 'dia' | 'semana' | 'mes';

export interface Usuario {
  id: string;
  nome: string;
  email: string;
  perfil: Perfil;
  ativo: boolean;
  dataCadastro: string;
  ultimoAcesso?: string | null;
  veterinarioId?: string | null;
  crmv?: string | null;
  especialidade?: string | null;
  tutorId?: string | null;
  telefone?: string | null;
  endereco?: string | null;
}

export interface RespostaLogin {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

export interface Tutor {
  id: string;
  usuarioId: string;
  nome: string;
  email: string;
  telefone: string;
  endereco: string;
  cpf: string;
  ativo: boolean;
  quantidadePets: number;
}

export interface Veterinario {
  id: string;
  usuarioId: string;
  nome: string;
  email: string;
  crmv: string;
  especialidade: string;
  ativo: boolean;
  /** HU-005: cor que diferencia o profissional na agenda geral. */
  cor: string;
}

export interface Pet {
  id: string;
  nome: string;
  especie: string;
  raca: string;
  sexo: string;
  pelagem: string;
  microchip: string;
  castrado: boolean;
  dataNascimento: string;
  idadeAnos: number;
  idadeDescritiva: string;
  pesoAtualKg?: number | null;
  tutorId: string;
  nomeTutor: string;
  telefoneTutor: string;
  ativo: boolean;
  dataObito?: string | null;
  alertasClinicos: AlergiaCondicao[];
  vacinasVencidas: number;
}

export type TipoAlerta = 'Alergia' | 'Comorbidade' | 'Restricao' | 'Cirurgia';
export type Gravidade = 'Leve' | 'Moderada' | 'Grave';

/** Alergia, comorbidade ou restrição que precisa ser vista antes de qualquer conduta. */
export interface AlergiaCondicao {
  id: string;
  pacienteId: string;
  tipo: TipoAlerta;
  descricao: string;
  gravidade: Gravidade;
  registradoPor: string;
  dataRegistro: string;
  ativa: boolean;
}

export type TipoVacina = 'Vacina' | 'Vermifugo' | 'Antipulgas' | 'Outro';
export type SituacaoDose = 'Em dia' | 'A vencer' | 'Vencida' | 'Dose única';

/** Item da carteira de vacinação e vermifugação. */
export interface Vacina {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  nomeTutor: string;
  tipo: TipoVacina;
  nome: string;
  fabricante: string;
  lote: string;
  dataAplicacao: string;
  proximaDose?: string | null;
  observacoes: string;
  aplicadaPor: string;
  situacaoDose: SituacaoDose;
  diasParaProximaDose?: number | null;
}

export interface ItemPrescricao {
  id: string;
  medicamento: string;
  dosagem: string;
  frequencia: string;
  duracao: string;
  via: string;
  observacao: string;
}

export interface Prescricao {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  especie: string;
  raca: string;
  nomeTutor: string;
  nomeVeterinario: string;
  crmv: string;
  dataEmissao: string;
  validaAte?: string | null;
  orientacoes: string;
  status: 'Ativa' | 'Cancelada';
  itens: ItemPrescricao[];
}

export interface BloqueioAgenda {
  id: string;
  veterinarioId: string;
  nomeVeterinario: string;
  inicio: string;
  fim: string;
  motivo: string;
  dataCriacao: string;
}

export interface RegistroAuditoria {
  id: string;
  usuarioId: string;
  nomeUsuario: string;
  perfil: string;
  acao: string;
  entidade: string;
  entidadeId?: string | null;
  detalhe: string;
  enderecoIp: string;
  dataHora: string;
}

export interface Tratamento {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  veterinarioId: string;
  nomeVeterinario: string;
  dataInicio: string;
  dataFim?: string | null;
  objetivoTerapeutico: string;
  status: string;
  observacoesGerais: string;
  totalSessoes: number;
  sessoesConcluidas: number;
}

export interface ItemAgenda {
  sessaoId: string;
  pacienteId: string;
  tratamentoId: string;
  veterinarioId: string;
  dataHora: string;
  nomePaciente: string;
  nomeTutor: string;
  nomeVeterinario: string;
  corVeterinario: string;
  status: StatusSessao;
  observacoes: string;
  possuiAtendimento: boolean;
}

export interface AgendaGeral {
  inicio: string;
  fim: string;
  veterinarios: Veterinario[];
  sessoes: ItemAgenda[];
  vazia: boolean;
}

export interface Sessao {
  id: string;
  tratamentoId: string;
  veterinarioId: string;
  nomeVeterinario: string;
  pacienteId: string;
  nomePaciente: string;
  nomeTutor: string;
  dataHora: string;
  status: StatusSessao;
  observacoes: string;
  possuiAtendimento: boolean;
  /** RN-009: já considera a antecedência mínima exigida pela clínica. */
  podeCancelar: boolean;
}

export interface Midia {
  id: string;
  sessaoId: string;
  atendimentoId?: string | null;
  tipo: 'Imagem' | 'Video';
  nomeArquivo: string;
  urlArquivo: string;
  dataUpload: string;
}

export interface ObservacaoInterna {
  id: string;
  prontuarioId: string;
  avaliacaoId?: string | null;
  atendimentoId?: string | null;
  autor: string;
  conteudo: string;
  dataRegistro: string;
}

export interface ItemLinhaTempo {
  id: string;
  data: string;
  tipo: 'Avaliação Clínica' | 'Atendimento' | 'Observação Interna';
  autor: string;
  descricao: string;
  detalhes: string;
  editado: boolean;
  escalaDor?: number | null;
  pesoKg?: number | null;
  campos: Record<string, string>;
  midias: Midia[];
  /** RN-003: vem sempre vazio quando o usuário é Tutor. */
  observacoesInternas: ObservacaoInterna[];
}

export interface PontoEvolucao {
  data: string;
  valor: number;
}

export interface Documento {
  id: string;
  prontuarioId: string;
  nomeArquivo: string;
  tipoDocumento: string;
  urlArquivo: string;
  tamanhoBytes: number;
  enviadoPor: string;
  dataUpload: string;
}

export interface Prontuario {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  especie: string;
  raca: string;
  sexo: string;
  pelagem: string;
  microchip: string;
  castrado: boolean;
  dataNascimento: string;
  idadeAnos: number;
  idadeDescritiva: string;
  nomeTutor: string;
  telefoneTutor: string;
  dataCriacao: string;
  ultimaAtualizacao: string;
  dataObito?: string | null;
  exibeObservacoesInternas: boolean;
  alertasClinicos: AlergiaCondicao[];
  historico: ItemLinhaTempo[];
  evolucaoPeso: PontoEvolucao[];
  evolucaoDor: PontoEvolucao[];
  vacinas: Vacina[];
  prescricoes: Prescricao[];
  documentos: Documento[];
  tratamentos: Tratamento[];
}

export interface Avaliacao {
  id: string;
  tratamentoId: string;
  prontuarioId: string;
  dataRegistro: string;
  dataUltimaEdicao?: string | null;
  nomeVeterinario: string;
  queixaPrincipal: string;
  anamnese: string;
  exameFisico: string;
  hipoteseDiagnostica: string;
  planoTerapeutico: string;
}

export interface Atendimento {
  id: string;
  sessaoId: string;
  tratamentoId: string;
  prontuarioId: string;
  dataRegistro: string;
  dataUltimaEdicao?: string | null;
  nomeVeterinario: string;
  tecnicasAplicadas: string;
  escalaDor: number;
  evolucaoClinica: string;
  sinaisVitais: string;
  proximosPassos: string;
  pesoKg?: number | null;
  temperaturaCelsius?: number | null;
  frequenciaCardiaca?: number | null;
  frequenciaRespiratoria?: number | null;
  midias: Midia[];
}

export interface Mensagem {
  id: string;
  remetenteId: string;
  nomeRemetente: string;
  destinatarioId: string;
  pacienteId?: string | null;
  nomePaciente?: string | null;
  conteudo: string;
  dataEnvio: string;
  lida: boolean;
  propria: boolean;
}

export interface Conversa {
  usuarioId: string;
  nome: string;
  perfil: Perfil;
  ultimaMensagem: string;
  dataUltimaMensagem: string;
  naoLidas: number;
  online: boolean;
}

export interface Notificacao {
  id: string;
  tipo: string;
  titulo: string;
  conteudo: string;
  linkRelacionado?: string | null;
  dataCriacao: string;
  visualizada: boolean;
}

export interface Indicadores {
  data: string;
  escopo: 'Clinica' | 'Veterinario';
  avaliacoesDoDia: number;
  atendimentosDoDia: number;
  sessoesDoDia: number;
  confirmacoesPendentes: number;
  pacientesAtivos: number;
  mensagensNaoLidas: number;
  notificacoesNaoVisualizadas: number;
  proximasSessoes: ItemAgenda[];
}

export interface RelatorioProdutividade {
  inicio: string;
  fim: string;
  totalAtendimentos: number;
  totalAvaliacoes: number;
  mediaEscalaDor: number;
  semRegistros: boolean;
  porVeterinario: {
    veterinarioId: string;
    nome: string;
    totalAtendimentos: number;
    mediaEscalaDor: number;
  }[];
  tecnicasMaisAplicadas: { tecnica: string; ocorrencias: number }[];
}

export interface Clinica {
  id: string;
  nome: string;
  cnpj: string;
  telefone: string;
  endereco: string;
  horasMinimasCancelamento: number;
  horarioAbertura: string;
  horarioFechamento: string;
  duracaoSessaoMinutos: number;
}

export interface HistoricoVersao {
  id: string;
  tipoRegistro: string;
  registroId: string;
  conteudoAnterior: string;
  alteradoPor: string;
  dataAlteracao: string;
}
