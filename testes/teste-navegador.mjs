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

  // ---- Esqueci minha senha: o e-mail traz só o código, e quem já o tem segue direto (sem
  // pedir outro, o que invalidaria o anterior). Não chama a API: não gasta o limite do login.
  await irPara(`/recuperar-senha`, 'networkidle2');
  await pagina.waitForSelector('#email');
  const botoesDaRecuperacao = await pagina.$$eval('button', (bs) => bs.map((b) => b.innerText.trim()));
  await pagina.type('#email', 'tutor@vetcare.com');
  await pagina.evaluate(() => {
    [...document.querySelectorAll('button')].find((b) => b.innerText.trim() === 'Já tenho um código')?.click();
  });
  const segueComOCodigo = await esperarTexto('Digite o código de 6 dígitos enviado para')
    .then(() => true)
    .catch(() => false);
  registrar(
    'Recuperação de senha por código, com a opção "Já tenho um código"',
    botoesDaRecuperacao.includes('Enviar código') && botoesDaRecuperacao.includes('Já tenho um código') && segueComOCodigo,
    botoesDaRecuperacao.join(' | '),
  );
  await capturar('01b-recuperar-senha-codigo');

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

    // A linha do tempo é neutra: o tipo de registro aparece pelo ícone e pelo nome, não por
    // uma cor diferente em cada categoria.
    const coresDaLinha = await pagina.evaluate(() => {
      const fundo = (el) => (el ? getComputedStyle(el).backgroundColor : '');
      return {
        marcadores: [...new Set([...document.querySelectorAll('ol li button > span[aria-hidden]')].map(fundo))],
        filtros: [...new Set([...document.querySelectorAll('button[aria-pressed]')].map(fundo))],
      };
    });
    registrar(
      'Linha do tempo sem cores por categoria',
      coresDaLinha.marcadores.length === 1 && coresDaLinha.filtros.length <= 1,
      `marcadores: ${coresDaLinha.marcadores.join(' / ')}; filtros: ${coresDaLinha.filtros.join(' / ')}`,
    );

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
      { prefixo: 'Avaliações', seletor: 'li[id^="avaliacao-"] button[aria-expanded]', captura: '05c-avaliacoes' },
      { prefixo: 'Tratamentos', seletor: 'li[id^="tratamento-"] button[aria-expanded]', captura: '05d-tratamentos' },
      { prefixo: 'Sessões', texto: 'As sessões cadastradas nos tratamentos', captura: '05j-sessoes' },
      { prefixo: 'Vacinação', texto: 'Carteira de vacinação', captura: '05a-vacinacao' },
      { prefixo: 'Medicação', texto: 'Receitas prescritas nas avaliações e nos tratamentos', captura: '05b-medicacao' },
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
      const abriu = await (esperada.seletor
        ? pagina.waitForSelector(esperada.seletor, { timeout: 8000 })
        : esperarTexto(esperada.texto)
      )
        .then(() => true)
        .catch(() => false);
      registrar(`HU-011 aba ${esperada.prefixo} renderiza`, abriu);
      if (abriu && esperada.captura) await capturar(esperada.captura);
    }

    // Avaliações em lista resumida: uma linha por avaliação, que expande com os detalhes.
    const indiceAvaliacoes = abas.findIndex((t) => t.startsWith('Avaliações'));
    if (indiceAvaliacoes >= 0) {
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indiceAvaliacoes].click();
      await pagina.waitForSelector('li[id^="avaliacao-"] button[aria-expanded]', { timeout: 8000 }).catch(() => null);
      const resumida = await pagina.evaluate(() => {
        const linhas = [...document.querySelectorAll('li[id^="avaliacao-"] button[aria-expanded]')];
        const fechadas = linhas.length > 0 && linhas.every((b) => b.getAttribute('aria-expanded') === 'false');
        const detalheAntes = document.body.textContent.includes('Motivo da consulta');
        linhas[0]?.click();
        return { fechadas, detalheAntes };
      });
      // O rótulo é exibido em caixa alta (CSS), por isso a busca é no textContent, não no innerText.
      const detalheDepois = await pagina
        .waitForFunction(() => document.body.textContent.includes('Motivo da consulta'), { timeout: 8000 })
        .then(() => true)
        .catch(() => false);
      registrar(
        'Avaliações: itens resumidos que expandem com os detalhes',
        resumida.fechadas && !resumida.detalheAntes && detalheDepois,
      );
      await capturar('05c-avaliacao-expandida');
    }

    // Tratamentos em lista resumida: o essencial numa linha; expandir mostra os detalhes, a
    // avaliação de origem e as sessões do tratamento.
    const indiceTratamentos = abas.findIndex((t) => t.startsWith('Tratamentos'));
    if (indiceTratamentos >= 0) {
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indiceTratamentos].click();
      await pagina.waitForSelector('li[id^="tratamento-"] button[aria-expanded]', { timeout: 8000 }).catch(() => null);
      const resumido = await pagina.evaluate(() => {
        const linhas = [...document.querySelectorAll('li[id^="tratamento-"] button[aria-expanded]')];
        const fechados = linhas.length > 0 && linhas.every((b) => b.getAttribute('aria-expanded') === 'false');
        const sessoesAntes = document.body.innerText.includes('Sessões do tratamento');
        linhas[0]?.click();
        return { fechados, sessoesAntes };
      });
      const expandiu = await esperarTexto('Sessões do tratamento').then(() => true).catch(() => false);
      const origem = await pagina.evaluate(() => document.body.innerText.includes('Avaliação de origem'));
      registrar(
        'Tratamentos: itens resumidos que expandem com a origem e as sessões',
        resumido.fechados && !resumido.sessoesAntes && expandiu && origem,
      );
      await capturar('05f-tratamento-expandido');

      // Receitas e pedidos de exame também nascem no próprio tratamento (ativo).
      const noTratamento = await pagina.evaluate(() => {
        const aberto = document.querySelector('li[id^="tratamento-"] button[aria-expanded="true"]')?.closest('li');
        const botoes = [...(aberto?.querySelectorAll('button') ?? [])].map((b) => b.innerText.trim());
        return {
          ativo: /Planejado|Em Andamento/.test(aberto?.innerText ?? ''),
          receita: botoes.includes('Emitir receita'),
          exames: botoes.includes('Pedir exames'),
          topicos: ['Plano do tratamento', 'Receitas e pedidos de exame'].every((t) => aberto?.innerText.includes(t)),
        };
      });
      registrar(
        'Tratamento ativo: emitir receita e pedir exames no próprio tratamento, com tópicos destacados',
        !noTratamento.ativo || (noTratamento.receita && noTratamento.exames && noTratamento.topicos),
        JSON.stringify(noTratamento),
      );

      // As sessões do tratamento aparecem em blocos: hoje, as próximas (a mais próxima
      // primeiro) e as anteriores (da mais recente para a mais antiga).
      const blocos = await pagina.evaluate(() => {
        const secoes = [...document.querySelectorAll('[id^="tratamento-"] section')].filter((s) => s.querySelector('h5'));
        return secoes.map((s) => ({
          titulo: s.querySelector('h5').innerText.replace(/\s*\(\d+\)$/, '').trim().toLowerCase(),
          datas: [...s.querySelectorAll('ol li')].map((li) => {
            const [, d, m, a] = li.innerText.match(/(\d{2})\/(\d{2})\/(\d{4})/) ?? [];
            return `${a}${m}${d}`;
          }),
        }));
      });
      const ordemDosBlocos = blocos.map((b) => b.titulo);
      const emOrdem = (lista, decrescente) =>
        lista.every((data, i) => i === 0 || (decrescente ? lista[i - 1] >= data : lista[i - 1] <= data));
      registrar(
        'Sessões do tratamento: hoje, próximas e anteriores (mais recentes primeiro)',
        blocos.length > 0 &&
          ordemDosBlocos.join(',') === ['hoje', 'próximas', 'anteriores'].filter((t) => ordemDosBlocos.includes(t)).join(',') &&
          blocos.every((b) => emOrdem(b.datas, b.titulo === 'anteriores')),
        blocos.map((b) => `${b.titulo}: ${b.datas.length}`).join(' · '),
      );

      // O cadastro é o plano: quantas sessões, a partir de quando, em que dias e em que horário.
      // A prévia mostra as datas antes de gravar. O mesmo botão acrescenta sessões depois.
      const abriuCadastro = await pagina.evaluate(() => {
        const botao = [...document.querySelectorAll('button')].find((b) =>
          ['Cadastrar sessões', 'Adicionar sessões'].includes(b.innerText.trim()),
        );
        botao?.click();
        return botao?.innerText.trim() ?? '';
      });
      const formulario = abriuCadastro
        ? await esperarTexto('Datas das sessões').then(() => true).catch(() => false)
        : false;
      let previa = { campos: false, datas: 0, botao: '' };
      if (formulario) {
        await pagina.evaluate(() => {
          const rotulo = [...document.querySelectorAll('[role="dialog"] label')].find((l) =>
            l.innerText.trim().startsWith('Quantidade'),
          );
          const campo = rotulo?.parentElement?.querySelector('input') ?? rotulo?.querySelector('input');
          Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(campo, '3');
          campo.dispatchEvent(new Event('input', { bubbles: true }));
        });
        await pagina
          .waitForFunction(() => document.querySelectorAll('[role="dialog"] section ol li').length === 3, { timeout: 8000 })
          .catch(() => null);
        previa = await pagina.evaluate(() => {
          const dialogo = document.querySelector('[role="dialog"]');
          const texto = dialogo.innerText;
          return {
            campos: ['Quantidade de sessões', 'A partir de', 'Dias da semana', 'Início', 'Término'].every((t) => texto.includes(t)),
            datas: dialogo.querySelectorAll('section ol li').length,
            botao: dialogo.querySelector('button[type="submit"]')?.innerText.trim() ?? '',
          };
        });
        await capturar('05h-cadastrar-sessoes');
        await pagina.keyboard.press('Escape');
      }
      registrar(
        'Tratamento: cadastro de sessões pela quantidade, dias e horário, com prévia das datas',
        formulario && previa.campos && previa.datas === 3 && previa.botao === 'Cadastrar 3 sessões',
        `botão "${abriuCadastro}", ${previa.datas} datas na prévia, "${previa.botao}"`,
      );
    }

    // Medicação e Exames: itens resumidos com a origem (avaliação ou tratamento), que expandem.
    for (const [prefixo, item, topico] of [
      ['Medicação', 'receita', 'Medicamento'],
      ['Exames', 'pedido', 'Exame'],
    ]) {
      const indice = abas.findIndex((t) => t.startsWith(prefixo));
      if (indice < 0) continue;
      const botoes = await pagina.$$('[role="tab"]');
      await botoes[indice].click();
      const temItens = await pagina
        .waitForSelector(`li[id^="${item}-"] button[aria-expanded]`, { timeout: 8000 })
        .then(() => true)
        .catch(() => false);
      if (!temItens) continue;
      const resumo = await pagina.evaluate((item) => {
        const linhas = [...document.querySelectorAll(`li[id^="${item}-"]`)];
        const fechados = linhas.every((li) => li.querySelector('button[aria-expanded]').getAttribute('aria-expanded') === 'false');
        const comOrigem = linhas.every((li) => /Avaliação|Tratamento/.test(li.innerText));
        const semSessao = linhas.every((li) => !/Sessão nº/.test(li.innerText));
        linhas[0].querySelector('button[aria-expanded]').click();
        return { fechados, comOrigem, semSessao, total: linhas.length };
      }, item);
      const expandiu = await pagina
        .waitForFunction(
          (item, topico) => {
            const aberto = document.querySelector(`li[id^="${item}-"] button[aria-expanded="true"]`)?.closest('li');
            const titulos = [...(aberto?.querySelectorAll('h4') ?? [])].map((h) => h.innerText.trim());
            return titulos.includes('Origem') && titulos.some((t) => t.startsWith(topico));
          },
          { timeout: 8000 },
          item,
          topico,
        )
        .then(() => true)
        .catch(() => false);
      registrar(
        `${prefixo}: itens resumidos com a origem, que expandem em tópicos`,
        resumo.fechados && resumo.comOrigem && resumo.semSessao && expandiu,
        `${resumo.total} itens`,
      );
      await capturar(item === 'receita' ? '05b-receita-expandida' : '05e-pedido-expandido');
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

  // ---- Sessões: a gestão das sessões cadastradas, separadas pela situação, sem cadastro
  await irPara(`/sessoes`, 'networkidle2');
  await esperarTexto('As sessões dos tratamentos da clínica');
  await pagina.waitForSelector('section[aria-label] li', { timeout: 8000 }).catch(() => null);
  const telaSessoes = await pagina.evaluate(() => ({
    tabela: document.querySelectorAll('section[aria-label] h3').length > 0 && !document.querySelector('table'),
    situacoes: [...document.querySelectorAll('[role="tab"]')].map((b) => b.innerText.replace(/\s*\d+$/, '').trim()),
    filtros: document.querySelectorAll('select, input[type="date"]').length,
    cadastro: [...document.querySelectorAll('button')].some((b) => /^(Registrar|Cadastrar|Agendar)/.test(b.innerText.trim())),
  }));
  registrar(
    'Tela de sessões organizada pela situação, agrupada por dia, com filtros',
    telaSessoes.tabela &&
      ['Todas', 'Agendadas', 'Aguardando registro', 'Realizadas', 'Canceladas', 'Inativadas'].every((s) =>
        telaSessoes.situacoes.includes(s),
      ) &&
      telaSessoes.filtros === 4,
    `${telaSessoes.situacoes.join(' | ')}; ${telaSessoes.filtros} filtros`,
  );
  registrar('Tela de sessões sem cadastro de sessão', !telaSessoes.cadastro);
  await capturar('05g-sessoes');

  // A aba da situação filtra a lista: em "Realizadas", toda linha é uma sessão realizada.
  await pagina.evaluate(() => {
    [...document.querySelectorAll('[role="tab"]')].find((b) => b.innerText.startsWith('Realizadas'))?.click();
  });
  const soRealizadas = await pagina
    .waitForFunction(
      () => {
        const linhas = [...document.querySelectorAll('section[aria-label] li')];
        return linhas.length > 0 && linhas.every((l) => l.innerText.includes('Realizada'));
      },
      { timeout: 8000 },
    )
    .then(() => true)
    .catch(() => false);
  registrar('Sessões: a aba Realizadas lista só as sessões realizadas', soRealizadas);
  await capturar('05i-sessoes-realizadas');

  // Do registro da sessão sai uma receita ou um pedido de exame: o veterinário escolhe o tipo,
  // preenche o formulário e vai para a aba do documento, que mostra o tratamento da sessão.
  const abriuRegistro = await pagina.evaluate(() => {
    const botao = document.querySelector('section[aria-label] li button[aria-label^="Ver o registro da sessão"]');
    botao?.click();
    return !!botao;
  });

  if (abriuRegistro) {
    const temBotaoUnico = await pagina
      .waitForFunction(
        () =>
          [...document.querySelectorAll('[role="dialog"] button')].some(
            (b) => b.innerText.trim() === 'Receita ou pedido de exame',
          ),
        { timeout: 8000 },
      )
      .then(() => true)
      .catch(() => false);
    const semBotaoAntigo = await pagina.evaluate(() => !document.body.innerText.includes('Emitir receita desta sessão'));
    registrar('Registro da sessão: um botão para receita ou pedido de exame', temBotaoUnico && semBotaoAntigo);

    await pagina.evaluate(() =>
      [...document.querySelectorAll('[role="dialog"] button')]
        .find((b) => b.innerText.trim() === 'Receita ou pedido de exame')
        ?.click(),
    );
    await esperarTexto('O que você vai emitir?');

    // A escolha mostra o formulário certo; trocar de opção não perde o que foi digitado.
    const escolher = (rotulo) =>
      pagina.evaluate(
        (r) => [...document.querySelectorAll('[role="dialog"] label')].find((l) => l.innerText.includes(r))?.click(),
        rotulo,
      );
    const visivel = (seletor, texto) =>
      pagina
        .waitForFunction(
          (s, t) =>
            [...document.querySelectorAll(`[role="dialog"] ${s}`)].some(
              (el) => el.getClientRects().length > 0 && (el.innerText || el.placeholder || '').includes(t),
            ),
          { timeout: 5000 },
          seletor,
          texto,
        )
        .then(() => true)
        .catch(() => false);

    await escolher('Receita de medicamento');
    const formReceita = await visivel('legend', 'Medicamento 1');
    await escolher('Pedido de exame');
    const formPedido = await visivel('textarea', 'Hemograma');
    const receitaEscondida = await pagina.evaluate(
      () =>
        ![...document.querySelectorAll('[role="dialog"] legend')].some(
          (l) => l.getClientRects().length > 0 && l.innerText.includes('Medicamento 1'),
        ),
    );
    registrar('Receita ou pedido: a escolha abre o formulário certo', formReceita && formPedido && receitaEscondida);
    await capturar('05j-receita-ou-exame');

    const campoExames = await pagina.evaluateHandle(() =>
      [...document.querySelectorAll('[role="dialog"] textarea')].find(
        (t) => t.getClientRects().length > 0 && t.placeholder.startsWith('Hemograma'),
      ),
    );
    await campoExames.asElement()?.type('Raio-X de controle do joelho');
    await pagina.evaluate(() =>
      [...document.querySelectorAll('[role="dialog"] button[type="submit"]')]
        .find((b) => b.getClientRects().length > 0 && b.innerText.includes('Emitir pedido'))
        ?.click(),
    );

    const foiParaExames = await pagina
      .waitForFunction(
        () =>
          location.pathname.startsWith('/prontuario/') &&
          location.search.includes('aba=exames') &&
          document.body.innerText.includes('Pedido de exame emitido'),
        { timeout: 10000 },
      )
      .then(() => true)
      .catch(() => false);
    registrar('Pedido emitido na sessão leva à aba Exames do prontuário', foiParaExames, pagina.url());

    const referencia = await pagina.evaluate(() => document.querySelector('li[id^="pedido-"]')?.innerText ?? '');
    registrar(
      'Pedido feito na sessão mostra o tratamento como origem',
      /Tratamento \S/.test(referencia) && !referencia.includes('Sessão nº'),
      referencia.split('\n').slice(0, 3).join(' / '),
    );
    await capturar('05k-exames-com-tratamento');
  } else {
    registrar('Registro da sessão: um botão para receita ou pedido de exame', false, 'nenhuma sessão realizada');
  }

  // ---- Agenda geral (HU-005)
  await irPara(`/agenda-geral`, 'networkidle2');
  await esperarTexto('Agenda geral');
  const temLegenda = await pagina.$$eval('button[aria-pressed]', (bs) => bs.length);
  registrar('HU-005 CA-2 toggle de profissionais', temLegenda >= 1, `${temLegenda} profissional(is)`);
  // Um cadastro só de sessões, no tratamento: a agenda geral não tem mais "Agendar sessão".
  const agendarNaAgendaGeral = await pagina.evaluate(() =>
    [...document.querySelectorAll('button')].some((b) => b.innerText.includes('Agendar sessão')),
  );
  registrar('Agenda geral sem o cadastro duplicado de sessões', !agendarNaAgendaGeral);
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
