/** A API grava em UTC; o aplicativo sempre apresenta no fuso do aparelho. */
function paraData(valor: string | Date): Date {
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
  });
}

export function diaDoMes(valor: string | Date): string {
  return String(paraData(valor).getDate()).padStart(2, '0');
}

export function mesAbreviado(valor: string | Date): string {
  return paraData(valor)
    .toLocaleDateString('pt-BR', { month: 'short' })
    .replace('.', '')
    .toUpperCase();
}

export function formatarPeso(valor?: number | null): string {
  return valor == null ? '—' : `${valor.toFixed(1).replace('.', ',')} kg`;
}

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

/**
 * Máscara de data para digitação no celular. Sem biblioteca de calendário, o campo é
 * um texto numérico que vai ganhando as barras conforme o tutor digita.
 */
export function mascaraDeData(texto: string): string {
  const numeros = texto.replace(/\D/g, '').slice(0, 8);

  if (numeros.length <= 2) return numeros;
  if (numeros.length <= 4) return `${numeros.slice(0, 2)}/${numeros.slice(2)}`;

  return `${numeros.slice(0, 2)}/${numeros.slice(2, 4)}/${numeros.slice(4)}`;
}

/**
 * Converte "DD/MM/AAAA" no formato ISO que a API espera, ou devolve null quando a data
 * não existe no calendário — o que pega tanto a digitação incompleta quanto 31/02.
 */
export function dataDigitadaParaIso(texto: string): string | null {
  const partes = texto.split('/');

  if (partes.length !== 3) return null;

  const [dia, mes, ano] = partes.map(Number);

  if (!dia || !mes || !ano || partes[2].length !== 4) return null;

  const data = new Date(ano, mes - 1, dia);

  const existeNoCalendario =
    data.getFullYear() === ano && data.getMonth() === mes - 1 && data.getDate() === dia;

  if (!existeNoCalendario) return null;

  const mesTexto = String(mes).padStart(2, '0');
  const diaTexto = String(dia).padStart(2, '0');

  return `${ano}-${mesTexto}-${diaTexto}`;
}

export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);

  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();

  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}
