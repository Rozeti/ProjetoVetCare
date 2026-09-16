import { mkdirSync } from 'node:fs';
import puppeteer from 'puppeteer-core';

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.env.WEB_URL ?? 'http://localhost:5173';
const PASTA = process.argv[2] ?? './capturas';
mkdirSync(PASTA, { recursive: true });

const erros = [];
const passos = [];

function registrar(nome, ok, detalhe = '') {
  passos.push({ nome, ok, detalhe });
  console.log(`${ok ? 'OK   ' : 'FALHA'} ${nome}${detalhe ? ` — ${detalhe}` : ''}`);
}

const API = process.env.API_URL ?? 'http://localhost:5265';

// Os roteiros de API terminam esgotando a janela do limitador de requisições, de
// propósito. Esperamos ela liberar para não confundir um 429 com credenciais inválidas.
for (let tentativa = 0; tentativa < 10; tentativa += 1) {
  const sonda = await fetch(`${API}/api/usuarios/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'sonda@vetcare.com', senha: 'sonda' }),
  });

  if (sonda.status !== 429) break;
  await new Promise((liberar) => setTimeout(liberar, 10_000));
}

const navegador = await puppeteer.launch({
  executablePath: EDGE,
  headless: 'new',
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
  defaultViewport: { width: 1440, height: 900 },
});

const pagina = await navegador.newPage();

pagina.on('console', (msg) => {
  if (msg.type() === 'error') erros.push(msg.text());
});
pagina.on('pageerror', (e) => erros.push(`pageerror: ${e.message}`));
pagina.on('response', (r) => {
  if (r.status() >= 400) erros.push(`HTTP ${r.status()} ${r.url()}`);
});

async function esperarTexto(texto, tempo = 8000) {
  await pagina.waitForFunction(
    (t) => document.body.innerText.includes(t),
    { timeout: tempo },
    texto,
  );
}

async function capturar(nome) {
  await pagina.screenshot({ path: `${PASTA}/${nome}.png`, fullPage: true });
}

/**
 * O aplicativo redireciona sozinho quando não há sessão, e esse redirecionamento pode
 * cancelar a navegação recém-pedida — o que o Chrome relata como ERR_ABORTED. A página chega
 * ao destino do mesmo jeito, então vale tentar de novo em vez de tratar como falha. Fica
 * evidente ao apontar o roteiro para o IP da rede, onde a latência é maior.
 */
async function irPara(caminho, espera = 'networkidle2') {
  for (let tentativa = 0; tentativa < 3; tentativa += 1) {
    try {
      await pagina.goto(`${BASE}${caminho}`, { waitUntil: espera });
      return;
    } catch (falha) {
      if (!String(falha.message).includes('ERR_ABORTED')) throw falha;
      await new Promise((liberar) => setTimeout(liberar, 500));
    }
  }
}

async function entrar(email, senha) {
  // A tela de login redireciona quem ja tem sessao, entao a limpeza vem antes.
  await irPara(`/`, 'domcontentloaded');
  await pagina.evaluate(() => {
    localStorage.clear();
  });
  await irPara(`/login`, 'networkidle2');
  await pagina.waitForSelector('#email');
  await pagina.type('#email', email);
  await pagina.type('#senha', senha);
  await Promise.all([
    pagina.click('button[type="submit"]'),
    pagina.waitForNavigation({ waitUntil: 'networkidle2' }).catch(() => {}),
  ]);
  await new Promise((r) => setTimeout(r, 1200));
}

try {
  // ---- Login inválido (HU-001, CA-2)
  await irPara(`/login`, 'networkidle2');
  await pagina.waitForSelector('#email');
  await pagina.type('#email', 'admin@vetcare.com');
  await pagina.type('#senha', 'senha-errada');
  await pagina.click('button[type="submit"]');
  await esperarTexto('inválidos');
  registrar('HU-001 CA-2 mensagem genérica de credenciais inválidas', true);
  await capturar('01-login-erro');

  // ---- Login do administrador
  await entrar('admin@vetcare.com', 'vetcare123');
  const urlAdmin = pagina.url();
  registrar('HU-001 CA-1 administrador entra no painel', !urlAdmin.includes('/login'), urlAdmin);
  await esperarTexto('Pacientes ativos');
  registrar('HU-016 painel exibe indicadores do dia', true);
  await capturar('02-painel-admin');

  // ---- Sidebar fixa (RNF-001)
  const menu = await pagina.$$eval('aside nav a', (as) => as.map((a) => a.textContent.trim()));
  registrar('RNF-001 sidebar fixa com navegação', menu.length >= 8, `${menu.length} itens: ${menu.join(', ')}`);

  // ---- Pacientes (HU-003)
  await irPara(`/pacientes`, 'networkidle2');
  await esperarTexto('Pacientes');
  const temTabela = await pagina.$('table');
  registrar('HU-003 lista de pacientes renderiza', !!temTabela);
  await capturar('03-pacientes');

  // ---- Prontuário (HU-011)
  // Abre o paciente de demonstração, que possui avaliação e atendimentos: o
  // gráfico de evolução só tem o que desenhar quando existem registros.
  const PACIENTE_COM_DADOS = process.env.PACIENTE_DEMO ?? 'Thor';

  const linkProntuario = await pagina
    .$$eval(
      'table tbody tr',
      (linhas, nome) => {
        const alvo =
          linhas.find((l) => l.innerText.includes(nome)) ?? linhas[0];
        return alvo?.querySelector('a')?.getAttribute('href') ?? null;
      },
      PACIENTE_COM_DADOS,
    )
    .catch(() => null);

  if (linkProntuario) {
    await irPara(`${linkProntuario}`, 'networkidle2');
    await esperarTexto('Linha do tempo');
    registrar('HU-011 CA-1 prontuário abre com linha do tempo', true);

    const abas = await pagina.$$eval('[role="tab"]', (bs) => bs.map((b) => b.textContent.trim()));
    registrar('HU-011 abas do prontuário', abas.length >= 4, abas.join(' | '));
    await capturar('04-prontuario');

    // Aba de evolução (HU-011, CA-3)
    const indiceEvolucao = abas.findIndex((t) => t.startsWith('Evolução'));
    if (indiceEvolucao >= 0) {
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indiceEvolucao].click();
      await esperarTexto('Evolução de peso');
      const temSvg = await pagina.$('svg[role="img"]');
      registrar('HU-011 CA-3 gráfico de evolução de peso', !!temSvg);
      await capturar('05-evolucao');
    }
  } else {
    registrar('HU-011 prontuário', false, 'nenhum paciente na tabela');
  }

  // ---- Agenda geral (HU-005)
  await irPara(`/agenda-geral`, 'networkidle2');
  await esperarTexto('Agenda geral');
  const temLegenda = await pagina.$$eval('button[aria-pressed]', (bs) => bs.length);
  registrar('HU-005 CA-2 toggle de profissionais', temLegenda >= 1, `${temLegenda} profissional(is)`);
  await capturar('06-agenda-geral');

  // ---- Usuários (HU-002)
  await irPara(`/usuarios`, 'networkidle2');
  await esperarTexto('Usuários');
  registrar('HU-002 tela de usuários acessível ao administrador', true);
  await capturar('07-usuarios');

  // ---- Relatórios (HU-017)
  await irPara(`/relatorios`, 'networkidle2');
  await esperarTexto('Relatórios de produtividade');
  registrar('HU-017 relatório de produtividade renderiza', true);
  await capturar('08-relatorios');

  // ---- Notificações (HU-015)
  await irPara(`/notificacoes`, 'networkidle2');
  await esperarTexto('Notificações');
  registrar('HU-015 tela de notificações renderiza', true);

  // ---- Mensagens (HU-014)
  await irPara(`/mensagens`, 'networkidle2');
  await esperarTexto('Mensagens');
  registrar('HU-014 tela de mensagens renderiza', true);
  await capturar('09-mensagens');

  // ---- Responsividade (RNF-006)
  await pagina.setViewport({ width: 390, height: 844 });
  await irPara(`/pacientes`, 'networkidle2');
  await new Promise((r) => setTimeout(r, 600));
  const semScrollHorizontal = await pagina.evaluate(
    () => document.documentElement.scrollWidth <= window.innerWidth + 2,
  );
  registrar('RNF-006 layout mobile sem rolagem horizontal', semScrollHorizontal);
  await capturar('10-mobile');
  await pagina.setViewport({ width: 1440, height: 900 });

  // ---- Perfil Tutor (HU-013 / RN-003)
  await entrar(process.env.EMAIL_TUTOR ?? 'tutor@vetcare.com', process.env.SENHA_TUTOR ?? 'vetcare123');
  const urlTutor = pagina.url();
  registrar('HU-001 tutor entra na própria área', urlTutor.includes('meus-pets'), urlTutor);
  await capturar('11-tutor-pets');

  const menuTutor = await pagina.$$eval('aside nav a', (as) => as.map((a) => a.textContent.trim()));
  const tutorVeUsuarios = menuTutor.some((t) => t.includes('Usuários'));
  const tutorVeRelatorios = menuTutor.some((t) => t.includes('Relatórios'));
  registrar(
    'RN-005 tutor não vê Usuários nem Relatórios no menu',
    !tutorVeUsuarios && !tutorVeRelatorios,
    menuTutor.join(', '),
  );

  // O tutor não deve conseguir alcançar /usuarios nem por URL direta.
  await irPara(`/usuarios`, 'networkidle2');
  await new Promise((r) => setTimeout(r, 800));
  registrar('RN-005 rota /usuarios redireciona o tutor', !pagina.url().endsWith('/usuarios'), pagina.url());

  // RN-003: o prontuário do tutor não pode conter observação interna.
  await irPara(`/meus-pets`, 'networkidle2');
  const linkPetTutor = await pagina.$eval('a[href^="/prontuario/"]', (a) => a.getAttribute('href')).catch(() => null);

  if (linkPetTutor) {
    await irPara(`${linkPetTutor}`, 'networkidle2');
    await new Promise((r) => setTimeout(r, 1000));
    const texto = await pagina.evaluate(() => document.body.innerText);
    const vazou = texto.includes('Observação interna — não visível ao tutor');
    registrar('RN-003 prontuário do tutor sem observações internas', !vazou);
    registrar(
      'HU-011 CA-2 aviso de visão filtrada do tutor',
      texto.includes('informações autorizadas do prontuário do seu pet'),
    );
    await capturar('12-tutor-prontuario');
  } else {
    registrar('RN-003 prontuário do tutor', false, 'tutor sem pets vinculados');
  }

  // ---- Logout (HU-001, CA-4)
  const botaoSair = await pagina.$$eval('button', (bs) =>
    bs.findIndex((b) => b.textContent.includes('Sair do sistema')),
  );

  if (botaoSair >= 0) {
    const botoes = await pagina.$$('button');
    await botoes[botaoSair].click();
    await new Promise((r) => setTimeout(r, 800));
    registrar('HU-001 CA-4 logout retorna ao login', pagina.url().includes('/login'), pagina.url());
  }
} catch (e) {
  registrar('execução do roteiro', false, e.message);
  await capturar('99-falha');
} finally {
  await navegador.close();
}

console.log('\n================================');
const ok = passos.filter((p) => p.ok).length;
const falhas = passos.filter((p) => !p.ok);
console.log(` OK: ${ok}   FALHAS: ${falhas.length}`);
console.log('================================');

if (erros.length) {
  console.log('\nErros de console capturados:');
  [...new Set(erros)].slice(0, 12).forEach((e) => console.log(' - ' + e));
}

process.exit(falhas.length === 0 ? 0 : 1);
