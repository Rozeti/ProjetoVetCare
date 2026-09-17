/** Contratos da VetCare.API usados pelo aplicativo do tutor. */

export type StatusSessao = 'Aguardando confirmação' | 'Confirmada' | 'Cancelada' | 'Concluída';

export interface Usuario {
  id: string;
  nome: string;
  email: string;
  perfil: 'Administrador' | 'Veterinario' | 'Tutor' | 'Apoio';
  ativo: boolean;
  dataCadastro: string;
  ultimoAcesso?: string | null;
  tutorId?: string | null;
  telefone?: string | null;
  endereco?: string | null;
}

export interface RespostaLogin {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

export type TipoAlerta = 'Alergia' | 'Comorbidade' | 'Restricao' | 'Cirurgia';
export type Gravidade = 'Leve' | 'Moderada' | 'Grave';

/** Alergia ou comorbidade que o tutor precisa conhecer. */
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
  ativo: boolean;
  dataObito?: string | null;
  alertasClinicos: AlergiaCondicao[];
  vacinasVencidas: number;
}

export type TipoVacina = 'Vacina' | 'Vermifugo' | 'Antipulgas' | 'Outro';
export type SituacaoDose = 'Em dia' | 'A vencer' | 'Vencida' | 'Dose única';

/** Item da carteira de vacinação do pet. */
export interface Vacina {
  id: string;
  pacienteId: string;
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
  nomeVeterinario: string;
  crmv: string;
  dataEmissao: string;
  validaAte?: string | null;
  orientacoes: string;
  status: 'Ativa' | 'Cancelada';
  itens: ItemPrescricao[];
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
  tipo: 'Imagem' | 'Video';
  nomeArquivo: string;
  urlArquivo: string;
  dataUpload: string;
}

export interface ItemLinhaTempo {
  id: string;
  data: string;
  tipo: string;
  autor: string;
  descricao: string;
  detalhes: string;
  editado: boolean;
  escalaDor?: number | null;
  pesoKg?: number | null;
  campos: Record<string, string>;
  midias: Midia[];
  /** RN-003: chega sempre vazio para o perfil Tutor. */
  observacoesInternas: unknown[];
}

export interface PontoEvolucao {
  data: string;
  valor: number;
}

export interface Documento {
  id: string;
  nomeArquivo: string;
  tipoDocumento: string;
  urlArquivo: string;
  tamanhoBytes: number;
  enviadoPor: string;
  dataUpload: string;
}

export interface Tratamento {
  id: string;
  nomeVeterinario: string;
  dataInicio: string;
  dataFim?: string | null;
  objetivoTerapeutico: string;
  status: string;
  observacoesGerais: string;
  totalSessoes: number;
  sessoesConcluidas: number;
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

export interface Conversa {
  usuarioId: string;
  nome: string;
  perfil: string;
  ultimaMensagem: string;
  dataUltimaMensagem: string;
  naoLidas: number;
  online: boolean;
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

/**
 * Recursos que a API anuncia quando algo muda no banco. O nome vem do controller
 * correspondente em minúsculas, então `/api/sessoes` publica em `sessoes`.
 */
export type RecursoAtualizado =
  | 'sessoes'
  | 'pets'
  | 'tratamentos'
  | 'atendimentos'
  | 'avaliacoes'
  | 'prescricoes'
  | 'vacinas'
  | 'alergias'
  | 'prontuarios'
  | 'documentos'
  | 'midias'
  | 'mensagens'
  | 'notificacoes'
  | (string & {});

/** Uma alteração já gravada no banco, avisada às telas abertas. */
export interface EventoAtualizacao {
  versao: number;
  recurso: RecursoAtualizado;
  acao: 'criado' | 'atualizado' | 'removido';
  /** Chega vazio no aplicativo do tutor: aqui basta saber que algo mudou (RN-003). */
  descricao: string;
  autor: string;
  propria: boolean;
  em: string;
}

export interface FeedAtualizacoes {
  versao: number;
  /** O aplicativo perdeu eventos demais e deve recarregar tudo. */
  reiniciar: boolean;
  eventos: EventoAtualizacao[];
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
