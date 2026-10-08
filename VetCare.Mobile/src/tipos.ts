/** Contratos da VetCare.API usados pelo aplicativo do tutor. */

export type Perfil = 'Administrador' | 'Veterinario' | 'Tutor' | 'Apoio';

export type StatusSessao = 'Aguardando confirmação' | 'Confirmada' | 'Cancelada' | 'Concluída';

export interface Usuario {
  id: string;
  nome: string;
  email: string;
  perfil: Perfil;
  ativo: boolean;
  dataCadastro: string;
  ultimoAcesso?: string | null;
  /** HU-015: canais pelos quais o usuário aceita ser avisado além do sistema. */
  notificarPorEmail: boolean;
  notificarPorPush: boolean;
  veterinarioId?: string | null;
  crmv?: string | null;
  especialidade?: string | null;
  tutorId?: string | null;
  telefone?: string | null;
  endereco?: string | null;
  cpf?: string | null;
}

export interface RespostaLogin {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

/** Resposta do pedido de redefinição; os campos de desenvolvimento só existem sem SMTP configurado. */
export interface RespostaRecuperacao {
  mensagem: string;
  tokenDesenvolvimento?: string | null;
  codigoDesenvolvimento?: string | null;
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
  telefoneTutor: string;
  /** Veterinário que acompanha o pet; vazio enquanto a clínica não designa ninguém. */
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel: string;
  ativo: boolean;
  dataObito?: string | null;
  alertasClinicos: AlergiaCondicao[];
  vacinasVencidas: number;
}

export type TipoVacina = 'Vacina' | 'Vermifugo' | 'Antipulgas' | 'Outro';
export type SituacaoDose = 'Em dia' | 'A vencer' | 'Vencida' | 'Dose única' | 'Concluída';

/** Intervalo do reforço; a API calcula a próxima dose a partir dele quando a data não é informada. */
export type RecorrenciaVacina = 'Nenhuma' | 'Mensal' | 'Trimestral' | 'Semestral' | 'Anual';

/** Item da carteira de vacinação do pet. */
export interface Vacina {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  nomeTutor: string;
  tipo: TipoVacina;
  nome: string;
  fabricante: string;
  lote: string;
  /** Posição e total do esquema ("Dose 2 de 3"); vazios numa aplicação avulsa. */
  numeroDose?: number | null;
  totalDoses?: number | null;
  descricaoDose: string;
  recorrencia: RecorrenciaVacina;
  dataAplicacao: string;
  proximaDose?: string | null;
  observacoes: string;
  veterinarioId?: string | null;
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
  /** URL assinada pela API, válida por algumas horas; recarregar a tela gera outra. */
  urlArquivo: string;
  dataUpload: string;
}

export type TipoItemLinhaTempo = 'Avaliação Clínica' | 'Atendimento' | 'Observação Interna';

export interface ItemLinhaTempo {
  id: string;
  data: string;
  tipo: TipoItemLinhaTempo;
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
  prontuarioId: string;
  nomeArquivo: string;
  tipoDocumento: string;
  /** URL assinada pela API, válida por algumas horas. */
  urlArquivo: string;
  tamanhoBytes: number;
  enviadoPor: string;
  dataUpload: string;
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
  /** Veterinário que acompanha o pet; vazio enquanto a clínica não designa ninguém. */
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel: string;
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

export interface Conversa {
  usuarioId: string;
  nome: string;
  perfil: Perfil;
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
  | 'tutores'
  | 'usuarios'
  | 'veterinarios'
  | 'clinica'
  | 'bloqueiosagenda'
  | 'observacoesinternas'
  | 'dispositivos'
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

/** Gatilhos de notificação da HU-015 e dos lembretes automáticos. */
export type TipoNotificacao =
  | 'SessaoAgendada'
  | 'LembreteConfirmacao'
  | 'StatusSessao'
  | 'NovoRegistroProntuario'
  | 'NovaMensagem'
  | 'DoseDeVacina'
  | 'PacienteTransferido';

export interface Notificacao {
  id: string;
  tipo: TipoNotificacao;
  titulo: string;
  conteudo: string;
  /** Caminho no portal web ("/minha-agenda", "/prontuario/{id}"); o app o traduz para as próprias telas. */
  linkRelacionado?: string | null;
  dataCriacao: string;
  visualizada: boolean;
}

/** Espécies oferecidas no cadastro; a API aceita qualquer texto. */
export const ESPECIES = ['Cachorro', 'Gato', 'Ave', 'Roedor', 'Outro'] as const;
