import type { Gravidade, ItemLinhaTempo, SituacaoDose, StatusSessao, TipoVacina } from '../types';

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

/**
 * Data de um evento da linha do tempo. A vacinação é um dia de calendário (ver `paraDia`) e
 * aparece sem hora; os demais eventos têm data e hora do registro.
 */
export function formatarDataDoEvento(item: Pick<ItemLinhaTempo, 'categoria' | 'data'>): string {
  return item.categoria === 'Vacinação' ? formatarDia(item.data) : formatarDataHora(item.data);
}

/** Dia do evento no calendário local ("AAAA-MM-DD"), para filtrar a linha do tempo por período. */
export function diaDoEvento(item: Pick<ItemLinhaTempo, 'categoria' | 'data'>): string {
  return item.categoria === 'Vacinação' ? paraValorInputDia(item.data) : paraValorInputData(item.data);
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

/** Corta um texto longo para caber numa linha de lista ou num item de seleção. */
export function resumirTexto(texto: string, limite: number): string {
  return texto.length > limite ? `${texto.slice(0, limite).trimEnd()}…` : texto;
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
  Planejado: 'bg-alerta-claro text-amber-800',
  'Em Andamento': 'bg-brand-100 text-brand-dark',
  'Concluído': 'bg-sucesso-claro text-emerald-800',
  Inativado: 'bg-slate-200 text-slate-700',
};

/** Planejado e Em Andamento aceitam sessões e evoluções; Concluído e Inativado, não. */
export function tratamentoAtivo(status: string): boolean {
  return status === 'Planejado' || status === 'Em Andamento';
}

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

/**
 * Formato mínimo de um e-mail (algo, @, algo), usado no login e na recuperação de senha:
 * a conta já existe, e quem confere as credenciais é a API.
 */
export function emailValido(valor: string): boolean {
  const texto = valor.trim();
  return texto.length > 0 && texto.length <= 180 && /^[^\s@]+@[^\s@]+$/.test(texto);
}

// ---------------------------------------------------------------------------------------
// Cadastro de pessoas: as mesmas regras de ValidacaoDeCadastro, na API. Cada função devolve
// a mensagem do problema, ou null; as que padronizam o formato devolvem também o valor.
// ---------------------------------------------------------------------------------------

/** Espaços repetidos viram um só, e as pontas são cortadas. */
export function compactar(texto: string): string {
  return texto.split(' ').filter(Boolean).join(' ').trim();
}

/** Nome completo: só letras (com acento), espaços, apóstrofos, hífens e pontos; nome e sobrenome. */
export function validarNome(nome: string): string | null {
  const texto = compactar(nome);

  if (texto.length < 3 || texto.length > 120) return 'O nome deve ter entre 3 e 120 caracteres.';
  if (!/^\p{L}[\p{L}\s'.-]*$/u.test(texto)) {
    return 'O nome deve ter apenas letras, espaços, apóstrofos, hífens e pontos (sem números).';
  }

  // Títulos abreviados ("Dra.") não contam como sobrenome.
  const palavras = texto
    .split(' ')
    .filter((p) => !p.endsWith('.') && (p.match(/\p{L}/gu)?.length ?? 0) >= 2).length;

  return palavras < 2 ? 'Informe o nome completo, com nome e sobrenome.' : null;
}

/** E-mail de cadastro: usuário, @ e domínio com extensão (nome@clinica.com.br). */
export function validarEmail(email: string): string | null {
  const texto = email.trim();

  if (!texto) return 'Informe o e-mail.';
  if (texto.length > 180) return 'O e-mail deve ter até 180 caracteres.';
  if (!/^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$/.test(texto) || texto.includes('..')) {
    return 'Informe um e-mail válido, como nome@clinica.com.br.';
  }

  return null;
}

/** Limites de senha da API (PasswordHasher). */
export const SENHA_MINIMA = 8;
export const SENHA_MAXIMA = 64;
export const DICA_SENHA = 'Mínimo de 8 caracteres, com letras e números.';

export function validarSenha(senha: string): string | null {
  if (senha.trim().length < SENHA_MINIMA) return `A senha deve ter no mínimo ${SENHA_MINIMA} caracteres, com letras e números.`;
  if (senha.length > SENHA_MAXIMA) return `A senha deve ter no máximo ${SENHA_MAXIMA} caracteres.`;
  if (!/\p{L}/u.test(senha) || !/\d/.test(senha)) return 'A senha precisa ter letras e números.';
  return null;
}

const UFS = new Set([
  'AC', 'AL', 'AM', 'AP', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MG', 'MS', 'MT', 'PA',
  'PB', 'PE', 'PI', 'PR', 'RJ', 'RN', 'RO', 'RR', 'RS', 'SC', 'SE', 'SP', 'TO',
]);

export interface Normalizado {
  valor?: string;
  erro?: string;
}

/** CRMV com UF e número: aceita "crmv df 12345", "DF-12345" ou "12345/DF" e devolve "CRMV-DF 12345". */
export function normalizarCrmv(crmv: string): Normalizado {
  const texto = crmv.trim().toUpperCase();

  if (!texto) return { erro: 'Informe o CRMV do veterinário.' };

  const antes = /^(?:CRMV)?[\s\-/]*([A-Z]{2})[\s\-/]*(\d{1,6})$/.exec(texto);
  const depois = antes ? null : /^(?:CRMV)?[\s\-/]*(\d{1,6})[\s\-/]*([A-Z]{2})$/.exec(texto);
  const uf = antes?.[1] ?? depois?.[2];
  const numero = antes?.[2] ?? depois?.[1];

  if (!uf || !numero) return { erro: 'Informe o CRMV no formato CRMV-UF 12345, por exemplo CRMV-DF 12345.' };
  if (!UFS.has(uf)) return { erro: `"${uf}" não é uma UF válida para o CRMV. Use a sigla do estado, como DF ou SP.` };

  return { valor: `CRMV-${uf} ${numero}` };
}

/** Só os dígitos de um texto. */
export function soDigitos(texto: string): string {
  return texto.replace(/\D/g, '');
}

/** Telefone com DDD: fixo (10 dígitos) ou celular (11, começando por 9). Devolve "(61) 99999-0000". */
export function normalizarTelefone(telefone: string, obrigatorio: boolean, rotulo = 'o telefone'): Normalizado {
  let digitos = soDigitos(telefone);

  if (!digitos) return obrigatorio ? { erro: `Informe ${rotulo} com DDD.` } : { valor: '' };
  if ((digitos.length === 12 || digitos.length === 13) && digitos.startsWith('55')) digitos = digitos.slice(2);

  const exemplo = 'Informe um telefone válido com DDD, como (61) 99999-0000 ou (61) 3333-4444.';

  if ((digitos.length !== 10 && digitos.length !== 11) || digitos[0] === '0' || digitos[1] === '0') return { erro: exemplo };
  if (digitos.length === 11 && digitos[2] !== '9') {
    return { erro: 'Celular com 11 dígitos começa com 9 depois do DDD, como (61) 99999-0000.' };
  }
  if (digitos.length === 10 && (digitos[2] === '0' || digitos[2] === '1')) return { erro: exemplo };

  const numero = digitos.slice(2);
  const corte = numero.length - 4;

  return { valor: `(${digitos.slice(0, 2)}) ${numero.slice(0, corte)}-${numero.slice(corte)}` };
}

/** Máscara do telefone enquanto o usuário digita: "(61) 99999-0000". */
export function mascararTelefone(texto: string): string {
  const digitos = soDigitos(texto).slice(0, 11);

  if (digitos.length <= 2) return digitos.length ? `(${digitos}` : '';

  const ddd = digitos.slice(0, 2);
  const numero = digitos.slice(2);

  if (numero.length <= 4) return `(${ddd}) ${numero}`;

  const corte = numero.length === 9 ? 5 : 4;
  return `(${ddd}) ${numero.slice(0, corte)}-${numero.slice(corte)}`;
}

function digitoDoCpf(digitos: string, quantidade: number): number {
  let soma = 0;
  for (let i = 0; i < quantidade; i += 1) soma += Number(digitos[i]) * (quantidade + 1 - i);
  const resto = soma % 11;
  return resto < 2 ? 0 : 11 - resto;
}

/** CPF opcional com os dígitos verificadores corretos. Devolve "000.000.000-00". */
export function normalizarCpf(cpf: string): Normalizado {
  const digitos = soDigitos(cpf);

  if (!digitos) return { valor: '' };

  if (
    digitos.length !== 11 ||
    /^(\d)\1{10}$/.test(digitos) ||
    digitoDoCpf(digitos, 9) !== Number(digitos[9]) ||
    digitoDoCpf(digitos, 10) !== Number(digitos[10])
  ) {
    return { erro: 'CPF inválido. Confira os 11 números.' };
  }

  return { valor: `${digitos.slice(0, 3)}.${digitos.slice(3, 6)}.${digitos.slice(6, 9)}-${digitos.slice(9)}` };
}

/** Máscara do CPF enquanto o usuário digita: "000.000.000-00". */
export function mascararCpf(texto: string): string {
  const d = soDigitos(texto).slice(0, 11);

  if (d.length <= 3) return d;
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** Endereço opcional; quando informado, com letras e entre 5 e 250 caracteres. */
export function validarEndereco(endereco: string): string | null {
  const texto = compactar(endereco);

  if (!texto) return null;
  if (texto.length < 5 || texto.length > 250 || !/\p{L}/u.test(texto)) {
    return 'Informe o endereço completo (rua, número e bairro), com até 250 caracteres.';
  }

  return null;
}

/** Texto descritivo opcional (especialidade, setor), escrito com letras. */
export function validarTextoOpcional(texto: string, rotulo: string, minimo: number, maximo: number): string | null {
  const valor = compactar(texto);

  if (!valor) return null;
  if (valor.length < minimo || valor.length > maximo) return `${rotulo} deve ter entre ${minimo} e ${maximo} caracteres.`;
  if ((valor.match(/\p{L}/gu)?.length ?? 0) < 2) return `${rotulo} deve ser escrito com letras.`;

  return null;
}

/** Início padrão do filtro de período da auditoria: os últimos 30 dias, inclusive hoje. */
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
