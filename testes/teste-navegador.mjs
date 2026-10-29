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
  // 401 no login inválido e 403 nas rotas que o roteiro testa de propósito são respostas
  // esperadas, não erros; o que interessa aqui são falhas que ninguém pediu. O favicon.ico
  // é pedido pelo navegador ao abrir a página estática usada para limpar a sessão.
  const esperado =
    (r.status() === 401 && r.url().includes('/usuarios/login')) ||
    r.status() === 403 ||
    (r.status() === 404 && r.url().endsWith('/favicon.ico'));
  if (r.status() >= 400 && !esperado) erros.push(`HTTP ${r.status()} ${r.url()}`);
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
  // A tela de login redireciona quem ja tem sessao, entao a limpeza vem antes. Ela e feita
  // numa pagina estatica da mesma origem: assim nenhuma tela do sistema esta montada e
  // nenhuma requisicao sai sem token enquanto o armazenamento e apagado.
  await irPara(`/favicon.svg`, 'domcontentloaded');
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

  // A lista só visualiza e inativa; o cadastro, a vacina e a transferência ficam no prontuário.
  const acoesPorLinha = await pagina
    .$$eval('table tbody tr', (linhas) =>
      linhas.map((l) => l.querySelectorAll('td:last-child a, td:last-child button').length),
    )
    .catch(() => []);
  registrar(
    'Pacientes: ações da lista reduzidas a visualizar e inativar',
    acoesPorLinha.length > 0 && acoesPorLinha.every((n) => n <= 2),
    `ações por linha: ${[...new Set(acoesPorLinha)].join(', ')}`,
  );
  const filtroRaca = await pagina.$('select[aria-label="Filtrar por raça"]');
  registrar('Pacientes: filtro por raça', !!filtroRaca);
  await capturar('03-pacientes');

  // ---- Prontuário (HU-011)
  // Abre o paciente de demonstração, que possui avaliação, tratamento e evoluções: a linha
  // do tempo e o tratamento só têm o que mostrar quando existem registros.
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

    // Linha do tempo: selecionar um evento abre o registro completo.
    const abriuRegistro = await pagina
      .evaluate(() => {
        const evento = document.querySelector('ol li button');
        evento?.click();
        return !!evento;
      })
      .catch(() => false);
    const registroVisivel = abriuRegistro
      ? await pagina.waitForSelector('[role="dialog"]', { timeout: 5000 }).then(() => true).catch(() => false)
      : false;
    registrar('Linha do tempo: o evento abre o registro completo', registroVisivel);
    if (registroVisivel) {
      await capturar('04a-registro-completo');
      await pagina.keyboard.press('Escape');
    }

    // A aba de gráficos saiu do prontuário, a de receitas passou a se chamar Medicação e as
    // sessões registradas (antes "evoluções") ganharam aba própria.
    registrar(
      'Prontuário sem a aba de gráficos e com as abas Sessões e Medicação',
      !abas.some((t) => t.startsWith('Gráficos')) &&
        abas.some((t) => t.startsWith('Sessões')) &&
        abas.some((t) => t.startsWith('Medicação')),
      abas.join(' | '),
    );

    // O que é clicável mostra a mão ao passar o mouse.
    const cursores = await pagina.evaluate(() => ({
      aba: getComputedStyle(document.querySelector('[role="tab"]')).cursor,
      botao: getComputedStyle([...document.querySelectorAll('button')].find((b) => !b.disabled)).cursor,
    }));
    registrar(
      'Itens clicáveis mostram a mão ao passar o mouse',
      cursores.aba === 'pointer' && cursores.botao === 'pointer',
      `aba: ${cursores.aba}, botão: ${cursores.botao}`,
    );

    // Linha do tempo: cada evento traz a data em que foi feito, e o período filtra a lista.
    const datasDosEventos = await pagina.$$eval('ol li button', (bs) =>
      bs.map((b) => b.querySelector('time')?.textContent?.trim() ?? ''),
    );
    registrar(
      'Linha do tempo: cada evento mostra a data em que foi feito',
      datasDosEventos.length > 0 && datasDosEventos.every((d) => /\d{2}\/\d{2}\/\d{4}/.test(d)),
      `${datasDosEventos.length} eventos, o primeiro em ${datasDosEventos[0] ?? '—'}`,
    );

    // O campo de data é controlado pelo React: o valor entra pelo setter nativo mais um "input".
    const filtrarDesde = (valor) =>
      pagina.evaluate((dia) => {
        const campo = document.querySelector('input[type="date"]');
        Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(campo, dia);
        campo.dispatchEvent(new Event('input', { bubbles: true }));
      }, valor);

    await filtrarDesde('2099-01-01');
    const periodoVazio = await esperarTexto('Nenhum evento no período escolhido').then(() => true).catch(() => false);
    await capturar('04b-linha-do-tempo-periodo');
    await filtrarDesde('');
    const listaDeVolta = await pagina
      .waitForFunction((total) => document.querySelectorAll('ol li button').length === total, { timeout: 5000 }, datasDosEventos.length)
      .then(() => true)
      .catch(() => false);
    registrar('Linha do tempo: filtro por período', periodoVazio && listaDeVolta);

    // As demais abas precisam abrir sem erro: cada uma tem um texto próprio de cabeçalho.
    const abasEsperadas = [
      { prefixo: 'Avaliações', texto: 'Avaliação de', captura: '05c-avaliacoes' },
      { prefixo: 'Tratamentos', texto: 'Sessões deste tratamento', captura: '05d-tratamentos' },
      { prefixo: 'Sessões', texto: 'Cada sessão de fisioterapia realizada, com o que foi feito', captura: '05j-sessoes' },
      { prefixo: 'Vacinação', texto: 'Carteira de vacinação', captura: '05a-vacinacao' },
      { prefixo: 'Medicação', texto: 'Receitas com os medicamentos prescritos', captura: '05b-medicacao' },
      { prefixo: 'Exames', texto: 'Pedidos de exame', captura: '05e-exames' },
      { prefixo: 'Documentos', texto: 'Documento', captura: null },
    ];

    for (const esperada of abasEsperadas) {
      const indice = abas.findIndex((t) => t.startsWith(esperada.prefixo));
      if (indice < 0) {
        registrar(`HU-011 aba ${esperada.prefixo}`, false, 'aba ausente');
        continue;
      }
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indice].click();
      const abriu = await esperarTexto(esperada.texto).then(() => true).catch(() => false);
      registrar(`HU-011 aba ${esperada.prefixo} renderiza`, abriu);
      if (abriu && esperada.captura) await capturar(esperada.captura);
    }

    // O tratamento nasce de uma avaliação e mostra só as evoluções dele.
    const indiceTratamentos = abas.findIndex((t) => t.startsWith('Tratamentos'));
    if (indiceTratamentos >= 0) {
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indiceTratamentos].click();
      await esperarTexto('Sessões deste tratamento');
      const doTratamento = await pagina.evaluate(() => ({
        origem: document.body.innerText.includes('Avaliação de origem'),
        expandiu: (() => {
          const botao = [...document.querySelectorAll('button[aria-expanded]')].find((b) =>
            b.innerText.includes('Sessões deste tratamento'),
          );
          botao?.click();
          return !!botao;
        })(),
      }));
      await new Promise((r) => setTimeout(r, 400));
      const evolucoesVisiveis = await pagina.evaluate(() => document.body.innerText.includes('Histórico completo do animal'));
      registrar(
        'Tratamento mostra a avaliação de origem e as próprias evoluções',
        doTratamento.origem && doTratamento.expandiu && evolucoesVisiveis,
      );
      await capturar('05f-tratamento-evolucoes');

      // A sessão de fisioterapia realizada é registrada no próprio tratamento e vira a
      // evolução seguinte; a sessão sem agendamento é sempre uma das opções.
      const abriuSessao = await pagina.evaluate(() => {
        const botao = [...document.querySelectorAll('button')].find((b) => b.innerText.trim() === 'Registrar sessão');
        botao?.click();
        return !!botao;
      });
      const formularioDaSessao = abriuSessao
        ? await esperarTexto('Registrar sessão realizada').then(() => true).catch(() => false)
        : false;
      const opcoesDeSessao = formularioDaSessao
        ? await pagina.$$eval('[role="dialog"] select option', (os) => os.map((o) => o.textContent.trim()))
        : [];
      registrar(
        'Tratamento: Registrar sessão abre a evolução com a escolha da sessão',
        opcoesDeSessao.some((o) => o.includes('sem agendamento')),
        opcoesDeSessao.join(' | '),
      );
      if (formularioDaSessao) {
        await capturar('05h-registrar-sessao');
        await pagina.keyboard.press('Escape');
      }
    }

    // Carteira com filtro e paginação: a aba Vacinação tem busca e seletor de situação.
    const indiceVacinas = abas.findIndex((t) => t.startsWith('Vacinação'));
    if (indiceVacinas >= 0) {
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indiceVacinas].click();
      await esperarTexto('Carteira de vacinação');
      const temFiltro = await pagina.$('select[aria-label="Filtrar por situação da dose"]');
      registrar('Carteira de vacinação com filtro de situação', !!temFiltro);
    }
  } else {
    registrar('HU-011 prontuário', false, 'nenhum paciente na tabela');
  }

  // ---- Sessões (antes "Evoluções"): filtros por animal, tratamento, período, veterinário e nº
  await irPara(`/sessoes`, 'networkidle2');
  await esperarTexto('Sessões de fisioterapia registradas');
  const telaEvolucoes = await pagina.evaluate(() => ({
    tabela: !!document.querySelector('table'),
    filtros: document.querySelectorAll('select, input[type="date"], input[type="number"]').length,
  }));
  registrar(
    'Tela de sessões com filtros e lista',
    telaEvolucoes.tabela && telaEvolucoes.filtros >= 5,
    `${telaEvolucoes.filtros} filtros`,
  );
  await capturar('05g-evolucoes');

  // O registro da sessão também começa por aqui: primeiro o animal e o tratamento.
  const abriuEscolha = await pagina.evaluate(() => {
    const botao = [...document.querySelectorAll('button')].find((b) => b.innerText.trim() === 'Registrar sessão');
    botao?.click();
    return !!botao;
  });
  const escolhaDoTratamento = abriuEscolha
    ? await esperarTexto('Escolha o animal e o tratamento').then(() => true).catch(() => false)
    : false;
  registrar('Sessões: Registrar sessão escolhe o animal e o tratamento', escolhaDoTratamento);
  if (escolhaDoTratamento) {
    await capturar('05i-evolucoes-registrar-sessao');
    await pagina.keyboard.press('Escape');
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

  // ---- Auditoria e configurações (somente administração)
  await irPara(`/auditoria`, 'networkidle2');
  await esperarTexto('Auditoria');
  registrar('Trilha de auditoria acessível ao administrador', true);
  const trilha = await pagina.evaluate(() => ({
    acoes: [...document.querySelectorAll('#acao option')].map((o) => o.textContent.trim()),
    texto: document.body.innerText,
  }));
  registrar(
    'Auditoria mostra só criações, alterações, inativações e exclusões',
    !trilha.acoes.some((a) => /login|consulta|download/i.test(a)) && !/\bLogin(Negado)?\b/.test(trilha.texto),
    trilha.acoes.join(', '),
  );
  await capturar('07b-auditoria');

  // Os tutores aparecem como os pacientes: em tabela, com a paginação no rodapé.
  await irPara(`/tutores`, 'networkidle2');
  await esperarTexto('Tutores');
  await pagina.waitForSelector('table', { timeout: 8000 }).catch(() => null);
  const listaDeTutores = await pagina.evaluate(() => ({
    tabela: !!document.querySelector('table'),
    paginacao: document.body.innerText.includes('Exibindo'),
  }));
  registrar('Tela de tutores lista em tabela com paginação', listaDeTutores.tabela && listaDeTutores.paginacao);
  const animaisDosTutores = await pagina.$$eval('table a[href^="/prontuario/"]', (as) => as.length).catch(() => 0);
  registrar('Tela de tutores mostra os animais vinculados', animaisDosTutores > 0, `${animaisDosTutores} animal(is)`);
  await capturar('07c-tutores');

  // Digitação contínua num formulário: o foco fica no campo a cada tecla, sem pular
  // para o botão de fechar a janela.
  await pagina.evaluate(() => {
    [...document.querySelectorAll('button')].find((b) => b.innerText.includes('Novo tutor'))?.click();
  });
  await esperarTexto('O cadastro cria também o acesso do tutor');
  await pagina.keyboard.type('Ana Beatriz', { delay: 40 });
  const digitado = await pagina.evaluate(() => ({
    elemento: document.activeElement?.tagName ?? '',
    valor: document.activeElement?.value ?? '',
  }));
  registrar(
    'Formulário mantém o foco no campo durante a digitação',
    digitado.elemento === 'INPUT' && digitado.valor === 'Ana Beatriz',
    `${digitado.elemento}: "${digitado.valor}"`,
  );
  await pagina.keyboard.press('Escape');

  // Novo usuário: tutores têm tela própria, então o perfil Tutor não é oferecido.
  await irPara(`/usuarios`, 'networkidle2');
  await pagina.evaluate(() => {
    [...document.querySelectorAll('button')].find((b) => b.innerText.includes('Novo usuário'))?.click();
  });
  await esperarTexto('Cadastro da equipe da clínica');
  const perfisOferecidos = await pagina.$$eval('[role="dialog"] select option', (os) => os.map((o) => o.textContent.trim()));
  registrar(
    'Novo usuário não oferece o perfil Tutor',
    perfisOferecidos.length >= 3 && !perfisOferecidos.some((p) => p.startsWith('Tutor')),
    perfisOferecidos.join(', '),
  );
  await pagina.keyboard.press('Escape');

  // Novo paciente: a clínica atende apenas cães, então não há escolha de espécie.
  await irPara(`/pacientes`, 'networkidle2');
  await pagina.evaluate(() => {
    [...document.querySelectorAll('button')].find((b) => b.innerText.includes('Novo paciente'))?.click();
  });
  await pagina.waitForSelector('[role="dialog"]', { timeout: 8000 });
  const temEspecie = await pagina.evaluate(() =>
    [...document.querySelectorAll('[role="dialog"] label')].some((l) => l.innerText.trim().startsWith('Espécie')),
  );
  registrar('Cadastro de paciente sem escolha de espécie (só cães)', !temEspecie);
  await capturar('07d-novo-paciente');
  await pagina.keyboard.press('Escape');

  await irPara(`/configuracoes`, 'networkidle2');
  await esperarTexto('Configurações da clínica');
  registrar('Configurações da clínica renderizam', true);

  // ---- Notificações (HU-015)
  await irPara(`/notificacoes`, 'networkidle2');
  await esperarTexto('Notificações');
  registrar('HU-015 tela de notificações renderiza', true);

  // ---- Mensagens (HU-014)
  await irPara(`/mensagens`, 'networkidle2');
  await esperarTexto('Mensagens');
  registrar('HU-014 tela de mensagens renderiza', true);

  // A conversa rola por dentro: a página não cresce conforme chegam mensagens.
  await pagina.evaluate(() => {
    document.querySelector('ul li button')?.click();
  });
  await new Promise((r) => setTimeout(r, 1200));
  const alturaDaPagina = await pagina.evaluate(() => ({
    pagina: document.documentElement.scrollHeight,
    janela: window.innerHeight,
  }));
  registrar(
    'Mensagens: só a conversa rola, a página cabe na janela',
    alturaDaPagina.pagina <= alturaDaPagina.janela + 80,
    `${alturaDaPagina.pagina}px de página para ${alturaDaPagina.janela}px de janela`,
  );
  await capturar('09-mensagens');

  // O chat não tem mais o seletor de pet, e o campo continua com o foco depois do Enter.
  let campoDoChat = await pagina.$('textarea[aria-label="Mensagem"]');
  if (!campoDoChat) {
    await pagina.evaluate(() => {
      [...document.querySelectorAll('button')].find((b) => b.innerText.includes('Nova conversa'))?.click();
    });
    await pagina.waitForSelector('[role="dialog"] ul li button', { timeout: 8000 }).catch(() => null);
    await pagina.evaluate(() => document.querySelector('[role="dialog"] ul li button')?.click());
    campoDoChat = await pagina.waitForSelector('textarea[aria-label="Mensagem"]', { timeout: 8000 }).catch(() => null);
  }
  if (campoDoChat) {
    const seletorDePet = await pagina.$('select[aria-label="Pet a que a mensagem se refere"]');
    const baloesAntes = await pagina.$$eval('p.whitespace-pre-wrap', (ps) => ps.length);
    await campoDoChat.click();
    await pagina.keyboard.type('Mensagem do roteiro de testes');
    await pagina.keyboard.press('Enter');
    const enviou = await pagina
      .waitForFunction((n) => document.querySelectorAll('p.whitespace-pre-wrap').length > n, { timeout: 8000 }, baloesAntes)
      .then(() => true)
      .catch(() => false);
    // Sem clicar de novo no campo, a próxima mensagem já pode ser digitada.
    await pagina.keyboard.type('x');
    const digitouDireto = await pagina.$eval('textarea[aria-label="Mensagem"]', (c) => c.value === 'x');
    registrar(
      'Chat: sem seletor de pet, e o campo segue com o foco depois do Enter',
      !seletorDePet && enviou && digitouDireto,
      `seletor de pet: ${seletorDePet ? 'presente' : 'ausente'}, enviou: ${enviou}, digitou direto: ${digitouDireto}`,
    );
    await capturar('09a-mensagem-enviada');
  } else {
    registrar('Chat: sem seletor de pet, e o campo segue com o foco depois do Enter', false, 'nenhuma conversa disponível');
  }

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
  const tutorVeAuditoria = menuTutor.some((t) => t.includes('Auditoria'));
  registrar(
    'RN-005 tutor não vê Usuários nem Auditoria no menu',
    !tutorVeUsuarios && !tutorVeAuditoria,
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
