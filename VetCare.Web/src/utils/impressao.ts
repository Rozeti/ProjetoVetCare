import type { Prescricao, Prontuario } from '../types';
import { formatarData, formatarDataHora } from './formato';

/**
 * Geração de documentos para impressão. Abrimos uma janela com HTML próprio em vez
 * de imprimir a tela: o documento clínico tem layout, cabeçalho e rodapé próprios,
 * e não deve carregar a navegação do sistema.
 */
function abrirParaImpressao(titulo: string, corpo: string) {
  const janela = window.open('', '_blank', 'width=900,height=700');

  if (!janela) {
    window.alert('Permita as janelas pop-up neste site para conseguir imprimir o documento.');
    return;
  }

  janela.document.write(`<!doctype html>
<html lang="pt-BR">
  <head>
    <meta charset="utf-8" />
    <title>${escapar(titulo)}</title>
    <style>${ESTILOS}</style>
  </head>
  <body>${corpo}</body>
</html>`);

  janela.document.close();
  janela.focus();

  // A impressão só é disparada depois do load para que as fontes e o layout
  // já estejam aplicados na prévia.
  janela.onload = () => janela.print();
}

const ESTILOS = `
  @page { size: A4; margin: 18mm 16mm; }
  * { box-sizing: border-box; }
  body {
    font-family: 'Segoe UI', system-ui, sans-serif;
    color: #0f172a;
    margin: 0;
    font-size: 12pt;
    line-height: 1.5;
  }
  header {
    border-bottom: 2px solid #0284c7;
    padding-bottom: 12px;
    margin-bottom: 20px;
  }
  header h1 { margin: 0; font-size: 20pt; color: #0284c7; }
  header p { margin: 2px 0 0; font-size: 10pt; color: #64748b; }
  h2 { font-size: 13pt; margin: 22px 0 8px; }
  .dados { display: flex; flex-wrap: wrap; gap: 8px 28px; font-size: 10.5pt; }
  .dados div { min-width: 150px; }
  .dados span { display: block; color: #64748b; font-size: 9pt; text-transform: uppercase; letter-spacing: .4px; }
  .item {
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    padding: 10px 14px;
    margin-bottom: 10px;
    page-break-inside: avoid;
  }
  .item strong { display: block; font-size: 12pt; }
  .item p { margin: 3px 0 0; font-size: 10.5pt; color: #475569; }
  .observacao { background: #f8fafc; border-left: 3px solid #0284c7; padding: 10px 14px; font-size: 10.5pt; }
  .alerta { background: #fee2e2; border-left: 3px solid #dc2626; padding: 10px 14px; margin-bottom: 14px; }
  table { width: 100%; border-collapse: collapse; font-size: 10.5pt; }
  th { text-align: left; background: #f1f5f9; padding: 7px 10px; font-size: 9.5pt; text-transform: uppercase; }
  td { padding: 7px 10px; border-top: 1px solid #e2e8f0; vertical-align: top; }
  tr { page-break-inside: avoid; }
  .assinatura { margin-top: 56px; text-align: center; page-break-inside: avoid; }
  .assinatura hr { width: 300px; border: none; border-top: 1px solid #0f172a; margin: 0 auto 6px; }
  .assinatura p { margin: 0; font-size: 10.5pt; }
  footer { margin-top: 32px; border-top: 1px solid #e2e8f0; padding-top: 8px; font-size: 8.5pt; color: #94a3b8; }
  @media print { body { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }
`;

function escapar(texto: string): string {
  return texto
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function cabecalho(nomeClinica: string, subtitulo: string): string {
  return `<header>
    <h1>${escapar(nomeClinica)}</h1>
    <p>${escapar(subtitulo)}</p>
  </header>`;
}

function rodape(): string {
  return `<footer>Documento gerado pelo VetCare em ${escapar(formatarDataHora(new Date()))}.</footer>`;
}

/** Receita pronta para entregar ao tutor, com espaço para assinatura do veterinário. */
export function imprimirReceita(prescricao: Prescricao, nomeClinica: string) {
  const itens = prescricao.itens
    .map(
      (item) => `<div class="item">
        <strong>${escapar(item.medicamento)}</strong>
        <p>${escapar([item.dosagem, item.frequencia, item.duracao, item.via].filter(Boolean).join(' · '))}</p>
        ${item.observacao ? `<p>${escapar(item.observacao)}</p>` : ''}
      </div>`,
    )
    .join('');

  const corpo = `
    ${cabecalho(nomeClinica, 'Receituário veterinário')}

    <div class="dados">
      <div><span>Paciente</span>${escapar(prescricao.nomePaciente)}</div>
      <div><span>Espécie / Raça</span>${escapar([prescricao.especie, prescricao.raca].filter(Boolean).join(' · '))}</div>
      <div><span>Tutor</span>${escapar(prescricao.nomeTutor)}</div>
      <div><span>Emissão</span>${escapar(formatarData(prescricao.dataEmissao))}</div>
      ${prescricao.validaAte ? `<div><span>Válida até</span>${escapar(formatarData(prescricao.validaAte))}</div>` : ''}
    </div>

    ${prescricao.status === 'Cancelada' ? '<div class="alerta"><strong>Receita cancelada.</strong> Este documento não tem validade.</div>' : ''}

    <h2>Prescrição</h2>
    ${itens}

    ${prescricao.orientacoes ? `<h2>Orientações</h2><div class="observacao">${escapar(prescricao.orientacoes)}</div>` : ''}

    <div class="assinatura">
      <hr />
      <p><strong>${escapar(prescricao.nomeVeterinario)}</strong></p>
      <p>${escapar(prescricao.crmv)}</p>
    </div>

    ${rodape()}`;

  abrirParaImpressao(`Receita — ${prescricao.nomePaciente}`, corpo);
}

/**
 * Prontuário completo em documento único. As observações internas não entram:
 * o que é impresso pode acabar nas mãos do tutor, e a RN-003 não admite exceções.
 */
export function imprimirProntuario(prontuario: Prontuario, nomeClinica: string) {
  const alertas = prontuario.alertasClinicos.length
    ? `<div class="alerta">
        <strong>Alertas clínicos</strong>
        ${prontuario.alertasClinicos
          .map((a) => `<p>${escapar(`${a.gravidade} · ${a.tipo}: ${a.descricao}`)}</p>`)
          .join('')}
      </div>`
    : '';

  const historico = prontuario.historico
    .filter((item) => item.tipo !== 'Observação Interna')
    .map(
      (item) => `<div class="item">
        <strong>${escapar(item.tipo)} — ${escapar(formatarDataHora(item.data))}</strong>
        <p>${escapar(item.autor)}</p>
        ${Object.entries(item.campos)
          .map(([rotulo, valor]) => `<p><strong>${escapar(rotulo)}:</strong> ${escapar(valor)}</p>`)
          .join('')}
      </div>`,
    )
    .join('');

  const vacinas = prontuario.vacinas.length
    ? `<h2>Carteira de vacinação</h2>
      <table>
        <thead><tr><th>Produto</th><th>Tipo</th><th>Aplicação</th><th>Próxima dose</th></tr></thead>
        <tbody>
          ${prontuario.vacinas
            .map(
              (v) => `<tr>
                <td>${escapar(v.nome)}</td>
                <td>${escapar(v.tipo)}</td>
                <td>${escapar(formatarData(v.dataAplicacao))}</td>
                <td>${v.proximaDose ? escapar(formatarData(v.proximaDose)) : '—'}</td>
              </tr>`,
            )
            .join('')}
        </tbody>
      </table>`
    : '';

  const corpo = `
    ${cabecalho(nomeClinica, 'Prontuário clínico')}

    <div class="dados">
      <div><span>Paciente</span>${escapar(prontuario.nomePaciente)}</div>
      <div><span>Espécie / Raça</span>${escapar([prontuario.especie, prontuario.raca].filter(Boolean).join(' · '))}</div>
      <div><span>Sexo</span>${escapar(prontuario.sexo || '—')}</div>
      <div><span>Idade</span>${escapar(prontuario.idadeDescritiva)}</div>
      ${prontuario.microchip ? `<div><span>Microchip</span>${escapar(prontuario.microchip)}</div>` : ''}
      <div><span>Tutor</span>${escapar(prontuario.nomeTutor)}</div>
    </div>

    ${alertas}

    <h2>Histórico clínico</h2>
    ${historico || '<p>Nenhum registro clínico até o momento.</p>'}

    ${vacinas}

    ${rodape()}`;

  abrirParaImpressao(`Prontuário — ${prontuario.nomePaciente}`, corpo);
}

/** Exportação de dados tabulares para planilha. */
export function baixarCsv(nomeArquivo: string, cabecalhos: string[], linhas: (string | number)[][]) {
  const escaparCampo = (valor: string | number) => {
    const texto = String(valor ?? '');
    return /[";\n]/.test(texto) ? `"${texto.replace(/"/g, '""')}"` : texto;
  };

  // Ponto e vírgula e BOM fazem o Excel em português abrir o arquivo já com as
  // colunas separadas e os acentos corretos.
  const conteudo = [cabecalhos, ...linhas]
    .map((linha) => linha.map(escaparCampo).join(';'))
    .join('\r\n');

  const blob = new Blob([`﻿${conteudo}`], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = nomeArquivo.endsWith('.csv') ? nomeArquivo : `${nomeArquivo}.csv`;
  link.click();

  URL.revokeObjectURL(url);
}
