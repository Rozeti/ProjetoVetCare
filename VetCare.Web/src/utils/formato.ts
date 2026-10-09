import type { Gravidade, SituacaoDose, StatusSessao, TipoVacina } from '../types';

/** A API grava tudo em UTC; a interface sempre apresenta no fuso local do usuário. */
export function paraData(valor: string | Date): Date {
  return valor instanceof Date ? valor : new Date(valor);
}

export function formatarData(valor: string | Date): string {
  return paraData(valor).toLocaleDateString('pt-BR');
}

export function formatarHora(valor: string | Date): string {
  return paraData(valor).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

export function formatarDataHora(valor: string | Date): string {
  return `${formatarData(valor)} às ${formatarHora(valor)}`;
}

export function formatarDataExtensa(valor: string | Date): string {
  return paraData(valor).toLocaleDateString('pt-BR', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric',
  });
}

/** Converte a data para o formato aceito por <input type="date">. */
export function paraValorInputData(valor: string | Date): string {
  const data = paraData(valor);
  const mes = String(data.getMonth() + 1).padStart(2, '0');
  const dia = String(data.getDate()).padStart(2, '0');

  return `${data.getFullYear()}-${mes}-${dia}`;
}

/**
 * Nascimento, óbito, aplicação e próxima dose de vacina e validade da receita são dias de
 * calendário, sem hora nem fuso: a API os envia como "AAAA-MM-DD". Passá-los por `new Date`
 * os leria como meia-noite UTC — que no fuso de Brasília ainda é o dia anterior. Aqui o dia
 * é montado no fuso local a partir dos números, e vale também para o formato antigo
 * ("2018-03-14T00:00:00Z"), cujo dia é o que vem antes do "T".
 */
export function paraDia(valor: string | Date): Date {
  if (valor instanceof Date) return valor;

  const partes = /^(\d{4})-(\d{2})-(\d{2})/.exec(valor);

  return partes ? new Date(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3])) : new Date(valor);
}

/** Exibe um dia de calendário (ver `paraDia`) sem deslocá-lo pelo fuso. */
export function formatarDia(valor: string | Date): string {
  return paraDia(valor).toLocaleDateString('pt-BR');
}

/** Preenche um <input type="date"> com um dia de calendário (ver `paraDia`). */
export function paraValorInputDia(valor: string | Date): string {
  return paraValorInputData(paraDia(valor));
}

/**
 * Junta data e hora locais e devolve em ISO. O horário digitado pelo usuário é
 * local; o `toISOString` faz a conversão para UTC que a API espera.
 */
export function paraIsoLocal(data: string, hora: string): string {
  return new Date(`${data}T${hora}:00`).toISOString();
}

export function formatarTamanho(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function formatarPeso(valor?: number | null): string {
  return valor == null ? '—' : `${valor.toFixed(1).replace('.', ',')} kg`;
}

/** Tempo relativo curto, usado em notificações e conversas. */
export function tempoRelativo(valor: string | Date): string {
  const minutos = Math.floor((Date.now() - paraData(valor).getTime()) / 60000);

  if (minutos < 1) return 'agora';
  if (minutos < 60) return `há ${minutos} min`;

  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `há ${horas} h`;

  const dias = Math.floor(horas / 24);
  if (dias < 7) return `há ${dias} d`;

  return formatarData(valor);
}

/** HU-004, CA-4: cada status da sessão tem tratamento visual próprio (badges). */
export const estiloStatusSessao: Record<StatusSessao, string> = {
  'Aguardando confirmação': 'bg-alerta-claro text-amber-800',
  Confirmada: 'bg-sucesso-claro text-emerald-800',
  Cancelada: 'bg-perigo-claro text-red-800',
  Concluída: 'bg-brand-100 text-brand-dark',
};

export const estiloStatusTratamento: Record<string, string> = {
  'Em Andamento': 'bg-brand-100 text-brand-dark',
  'Concluído': 'bg-sucesso-claro text-emerald-800',
  Interrompido: 'bg-slate-200 text-slate-700',
};

/** Escala de dor 0–10: verde quando baixa, âmbar em nível médio, vermelha quando alta. */
export function estiloEscalaDor(valor: number): string {
  if (valor <= 3) return 'bg-sucesso-claro text-emerald-800';
  if (valor <= 6) return 'bg-alerta-claro text-amber-800';
  return 'bg-perigo-claro text-red-800';
}

export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);

  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();

  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}

/** Alertas clínicos: a gravidade define o destaque (mesmas cores no prontuário, nas listas e no modal). */
export const estiloGravidade: Record<Gravidade, string> = {
  Grave: 'bg-perigo-claro text-red-800 border-red-300',
  Moderada: 'bg-alerta-claro text-amber-800 border-amber-300',
  Leve: 'bg-slate-100 text-slate-700 border-slate-300',
};

export const rotuloTipoAlerta: Record<string, string> = {
  Alergia: 'Alergia',
  Comorbidade: 'Comorbidade',
  Restricao: 'Restrição',
  Cirurgia: 'Cirurgia',
};

/** Carteira de vacinação: situação da dose e nome legível do tipo de produto. */
export const estiloSituacaoDose: Record<SituacaoDose, string> = {
  Vencida: 'bg-perigo-claro text-red-800',
  'A vencer': 'bg-alerta-claro text-amber-800',
  'Em dia': 'bg-sucesso-claro text-emerald-800',
  'Dose única': 'bg-slate-100 text-slate-600',
  Concluída: 'bg-brand-100 text-brand-dark',
};

export const rotuloTipoVacina: Record<TipoVacina, string> = {
  Vacina: 'Vacina',
  Vermifugo: 'Vermífugo',
  Antipulgas: 'Antipulgas',
  Outro: 'Outro',
};

/** Mesma verificação do [EmailAddress] da API: um "@" com algo antes e depois, sem espaços. */
export function emailValido(valor: string): boolean {
  const texto = valor.trim();
  return texto.length > 0 && texto.length <= 180 && /^[^\s@]+@[^\s@]+$/.test(texto);
}

/** Limites de senha da API (PasswordHasher). */
export const SENHA_MINIMA = 6;
export const SENHA_MAXIMA = 64;

export function validarSenha(senha: string): string | null {
  if (senha.length < SENHA_MINIMA) return `A senha deve ter no mínimo ${SENHA_MINIMA} caracteres.`;
  if (senha.length > SENHA_MAXIMA) return `A senha deve ter no máximo ${SENHA_MAXIMA} caracteres.`;
  return null;
}

/** Início padrão dos filtros de período (relatórios e auditoria): os últimos 30 dias, inclusive hoje. */
export function trintaDiasAtras(): string {
  const data = new Date();
  data.setDate(data.getDate() - 29);
  return paraValorInputData(data);
}

/** Limites do cadastro de paciente (CriarPetDTO / CriarPetDoTutorDTO / AtualizarPetDTO). */
export const LIMITES_DO_PET = { nome: 80, especie: 40, raca: 80, sexo: 20, pelagem: 60, microchip: 40, pesoMinimo: 0.1, pesoMaximo: 200 };

/**
 * Confere os dados de um pet antes de enviar, com as mesmas regras da API: nome e
 * data de nascimento obrigatórios, data real e não futura, peso dentro da faixa e
 * textos dentro das colunas. Devolve a mensagem do primeiro problema, ou null.
 */
export function validarDadosDoPet(dados: {
  nome: string;
  especie: string;
  raca: string;
  sexo?: string;
  pelagem: string;
  microchip: string;
  dataNascimento: string;
  pesoAtualKg: string;
}): string | null {
  const nome = dados.nome.trim();

  if (!nome) return 'Informe o nome do pet.';
  if (nome.length > LIMITES_DO_PET.nome) return `O nome deve ter até ${LIMITES_DO_PET.nome} caracteres.`;
  if (!dados.especie.trim()) return 'Informe a espécie.';
  if (dados.especie.trim().length > LIMITES_DO_PET.especie) return `A espécie deve ter até ${LIMITES_DO_PET.especie} caracteres.`;
  if (dados.raca.trim().length > LIMITES_DO_PET.raca) return `A raça deve ter até ${LIMITES_DO_PET.raca} caracteres.`;
  if ((dados.sexo ?? '').trim().length > LIMITES_DO_PET.sexo) return `O sexo deve ter até ${LIMITES_DO_PET.sexo} caracteres.`;
  if (dados.pelagem.trim().length > LIMITES_DO_PET.pelagem) return `A pelagem deve ter até ${LIMITES_DO_PET.pelagem} caracteres.`;
  if (dados.microchip.trim().length > LIMITES_DO_PET.microchip) return `O microchip deve ter até ${LIMITES_DO_PET.microchip} caracteres.`;

  if (!dados.dataNascimento) return 'Informe a data de nascimento.';

  const nascimento = new Date(`${dados.dataNascimento}T12:00:00`);

  if (Number.isNaN(nascimento.getTime())) return 'A data de nascimento não é válida.';
  if (dados.dataNascimento > paraValorInputData(new Date())) return 'A data de nascimento não pode ser futura.';

  if (dados.pesoAtualKg.trim()) {
    const peso = Number(dados.pesoAtualKg.replace(',', '.'));

    if (!Number.isFinite(peso) || peso < LIMITES_DO_PET.pesoMinimo || peso > LIMITES_DO_PET.pesoMaximo) {
      return `Informe um peso entre ${LIMITES_DO_PET.pesoMinimo} e ${LIMITES_DO_PET.pesoMaximo} kg.`;
    }
  }

  return null;
}
