import type { StatusSessao } from '../types';

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
