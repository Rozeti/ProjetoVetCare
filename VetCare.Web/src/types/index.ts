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
  /** HU-015: canais pelos quais o usuário aceita ser avisado além do sistema. */
  notificarPorEmail: boolean;
  notificarPorPush: boolean;
  veterinarioId?: string | null;
  /** Preenchido quando o usuário é Apoio administrativo. */
  setor?: string | null;
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
  /** Animais vinculados ao tutor, no mesmo recorte de quem consulta (RN-008). */
  pets: PetDoTutor[];
}

export interface PetDoTutor {
  id: string;
  nome: string;
  especie: string;
  ativo: boolean;
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
  /** Veterinário que acompanha o paciente; nulo quando a clínica ainda não designou ninguém. */
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel: string;
  ativo: boolean;
  dataObito?: string | null;
  alertasClinicos: AlergiaCondicao[];
  vacinasVencidas: number;
}

/** Paciente ativo na lista de seleção dos formulários (agendamento, mensagens), sem paginação. */
export interface PetSelecao {
  id: string;
  nome: string;
  especie: string;
  nomeTutor: string;
  tutorId: string;
  /** Usuário de acesso do tutor, para ligar o paciente a uma conversa. */
  tutorUsuarioId: string;
  veterinarioResponsavelId?: string | null;
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
export type SituacaoDose = 'Em dia' | 'A vencer' | 'Vencida' | 'Dose única' | 'Concluída';

/** Intervalo do reforço; a API calcula a próxima dose a partir dele quando a data não é informada. */
export type RecorrenciaVacina = 'Nenhuma' | 'Mensal' | 'Trimestral' | 'Semestral' | 'Anual';

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
  /** Origem da receita: a avaliação ou a evolução em que foi prescrita. */
  avaliacaoId?: string | null;
  atendimentoId?: string | null;
  /** "Avaliação de 12/04/2026" ou "Evolução nº 3 — RPG"; vazio nas receitas antigas sem origem. */
  origem: string;
  itens: ItemPrescricao[];
}

/** Pedido de exames gerado a partir de uma avaliação. */
export interface PedidoExame {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  especie: string;
  raca: string;
  nomeTutor: string;
  avaliacaoId: string;
  dataAvaliacao?: string | null;
  motivoAvaliacao: string;
  /** Quem assinou: o veterinário só edita e exclui os próprios pedidos. */
  veterinarioId: string;
  nomeVeterinario: string;
  crmv: string;
  dataEmissao: string;
  /** Exames solicitados, um por linha. */
  exames: string;
  justificativa: string;
  /** "Cancelado" só nos pedidos antigos: hoje um pedido que não serve mais é excluído. */
  status: 'Ativo' | 'Cancelado';
  motivoCancelamento: string;
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

export type StatusTratamento = 'Planejado' | 'Em Andamento' | 'Concluído' | 'Inativado';

/** Tratamento: plano terapêutico encaminhado a partir de uma avaliação. */
export interface Tratamento {
  id: string;
  pacienteId: string;
  nomePaciente: string;
  /** Avaliação de origem. */
  avaliacaoId: string;
  dataAvaliacao?: string | null;
  motivoAvaliacao: string;
  veterinarioId: string;
  nomeVeterinario: string;
  nome: string;
  tipo: string;
  descricao: string;
  duracaoPrevista: string;
  frequencia: string;
  sessoesPrevistas: number;
  dataInicio: string;
  dataFim?: string | null;
  status: StatusTratamento;
  observacoesGerais: string;
  /** Sessões agendadas para o tratamento. */
  totalSessoes: number;
  sessoesConcluidas: number;
  /** Evoluções (sessões realizadas) registradas. */
  evolucoesRegistradas: number;
}

export interface TratamentoResumo {
  id: string;
  nome: string;
  status: StatusTratamento;
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

export interface ObservacaoInterna {
  id: string;
  prontuarioId: string;
  avaliacaoId?: string | null;
  atendimentoId?: string | null;
  autor: string;
  conteudo: string;
  dataRegistro: string;
}

export type CategoriaLinhaTempo =
  | 'Avaliação'
  | 'Tratamento'
  | 'Sessão'
  | 'Cirurgia'
  | 'Vacinação'
  | 'Receita'
  | 'Exame'
  | 'Observação';

/** Evento da linha do tempo clínica do animal. */
export interface ItemLinhaTempo {
  /** Identificador do registro de origem (avaliação, tratamento, evolução...). */
  id: string;
  /** Única por evento: um tratamento gera o evento de início e o de encerramento. */
  chave: string;
  data: string;
  categoria: CategoriaLinhaTempo;
  /** Título do evento, por exemplo "Avaliação clínica" ou "Tratamento concluído". */
  tipo: string;
  /** Responsável pelo registro. */
  autor: string;
  descricao: string;
  detalhes: string;
  editado: boolean;
  tratamentoId?: string | null;
  numeroSessao?: number | null;
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
  /** URL assinada pela API, válida por algumas horas. */
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
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel: string;
  dataCriacao: string;
  ultimaAtualizacao: string;
  dataObito?: string | null;
  exibeObservacoesInternas: boolean;
  alertasClinicos: AlergiaCondicao[];
  /** Avaliações do animal, cada uma com os tratamentos que originou. */
  avaliacoes: Avaliacao[];
  historico: ItemLinhaTempo[];
  evolucaoPeso: PontoEvolucao[];
  evolucaoDor: PontoEvolucao[];
  vacinas: Vacina[];
  prescricoes: Prescricao[];
  pedidosExame: PedidoExame[];
  documentos: Documento[];
  tratamentos: Tratamento[];
}

/** Primeiro contato clínico de uma consulta; pode originar tratamentos, receitas e pedidos de exame. */
export interface Avaliacao {
  id: string;
  pacienteId: string;
  prontuarioId: string;
  dataRegistro: string;
  dataUltimaEdicao?: string | null;
  veterinarioId: string;
  nomeVeterinario: string;
  motivoConsulta: string;
  observacoes: string;
  diagnosticoPossivel: string;
  tratamentos: TratamentoResumo[];
  quantidadeReceitas: number;
  quantidadePedidosExame: number;
}

/** Evolução: o registro de uma sessão realizada dentro de um tratamento. */
export interface Atendimento {
  id: string;
  sessaoId: string;
  tratamentoId: string;
  nomeTratamento: string;
  pacienteId: string;
  nomePaciente: string;
  prontuarioId: string;
  numeroSessao: number;
  dataRegistro: string;
  dataUltimaEdicao?: string | null;
  veterinarioId: string;
  nomeVeterinario: string;
  /** Procedimentos realizados. */
  tecnicasAplicadas: string;
  escalaDor: number;
  /** Descrição do que foi realizado. */
  evolucaoClinica: string;
  reacaoAnimal: string;
  observacoes: string;
  relatorio: string;
  sinaisVitais: string;
  proximosPassos: string;
  pesoKg?: number | null;
  temperaturaCelsius?: number | null;
  frequenciaCardiaca?: number | null;
  frequenciaRespiratoria?: number | null;
  midias: Midia[];
}

/** Linha da tela de evoluções. */
export interface EvolucaoResumo {
  id: string;
  sessaoId: string;
  tratamentoId: string;
  nomeTratamento: string;
  pacienteId: string;
  nomePaciente: string;
  numeroSessao: number;
  dataRegistro: string;
  veterinarioId: string;
  nomeVeterinario: string;
  tecnicasAplicadas: string;
  evolucaoClinica: string;
  reacaoAnimal: string;
  escalaDor: number;
  editado: boolean;
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

/**
 * Recursos que a API anuncia quando algo muda no banco. O nome vem do controller
 * correspondente em minúsculas, então `/api/sessoes` publica em `sessoes`.
 */
export type RecursoAtualizado =
  | 'sessoes'
  | 'pets'
  | 'tutores'
  | 'veterinarios'
  | 'usuarios'
  | 'tratamentos'
  | 'atendimentos'
  | 'avaliacoes'
  | 'prescricoes'
  | 'vacinas'
  | 'alergias'
  | 'prontuarios'
  | 'documentos'
  | 'midias'
  | 'observacoesinternas'
  | 'bloqueiosagenda'
  | 'mensagens'
  | 'notificacoes'
  | 'clinica'
  | (string & {});

/** Uma alteração já gravada no banco, avisada às telas abertas. */
export interface EventoAtualizacao {
  versao: number;
  recurso: RecursoAtualizado;
  acao: 'criado' | 'atualizado' | 'removido';
  /** Vem vazio para o perfil Tutor, que recebe apenas o aviso de que algo mudou. */
  descricao: string;
  autor: string;
  /** Verdadeiro quando foi o próprio usuário quem provocou a alteração. */
  propria: boolean;
  em: string;
}

export interface FeedAtualizacoes {
  versao: number;
  /** A tela perdeu eventos demais e deve recarregar tudo. */
  reiniciar: boolean;
  eventos: EventoAtualizacao[];
}

export interface HistoricoVersao {
  id: string;
  tipoRegistro: string;
  registroId: string;
  conteudoAnterior: string;
  alteradoPor: string;
  dataAlteracao: string;
}

/** A clínica atende apenas cães: é a espécie de todo paciente novo. */
export const ESPECIE_ATENDIDA = 'Cachorro';

/** Sugestões do campo de raça; a API aceita qualquer texto. */
export const RACAS_DE_CAES = [
  'SRD (sem raça definida)',
  'Beagle',
  'Border Collie',
  'Buldogue Francês',
  'Buldogue Inglês',
  'Chihuahua',
  'Cocker Spaniel',
  'Dachshund',
  'Golden Retriever',
  'Husky Siberiano',
  'Labrador Retriever',
  'Lhasa Apso',
  'Maltês',
  'Pastor Alemão',
  'Pinscher',
  'Pit Bull',
  'Poodle',
  'Pug',
  'Rottweiler',
  'Schnauzer',
  'Shih Tzu',
  'Spitz Alemão',
  'Yorkshire Terrier',
] as const;
