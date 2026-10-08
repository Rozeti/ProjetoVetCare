import { useCallback, useState, type FormEvent } from 'react';
import { Loader2, Pencil, Plus, Search, Syringe, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useCarregamento } from '../../hooks/useCarregamento';
import { useAtualizacao } from '../../contexts/atualizacoes';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { PaginaDe, RecorrenciaVacina, SituacaoDose, TipoVacina, Vacina } from '../../types';
import { Alerta, Campo, Card, Carregando, Etiqueta, Modal, SemDados } from '../../components/ui';
import { Paginacao } from '../../components/Paginacao';
import { SeletorVeterinario } from '../../components/SeletorVeterinario';
import { estiloSituacaoDose, formatarData, paraValorInputData, rotuloTipoVacina } from '../../utils/formato';
import { paginaVazia } from '../../utils/paginacao';

const TIPOS: TipoVacina[] = ['Vacina', 'Vermifugo', 'Antipulgas', 'Outro'];
const SITUACOES: SituacaoDose[] = ['Vencida', 'A vencer', 'Em dia', 'Dose única', 'Concluída'];

const RECORRENCIAS: { valor: RecorrenciaVacina; rotulo: string }[] = [
  { valor: 'Nenhuma', rotulo: 'Sem recorrência' },
  { valor: 'Mensal', rotulo: 'Mensal' },
  { valor: 'Trimestral', rotulo: 'Trimestral (a cada 3 meses)' },
  { valor: 'Semestral', rotulo: 'Semestral (a cada 6 meses)' },
  { valor: 'Anual', rotulo: 'Anual' },
];

const ROTULO_RECORRENCIA: Record<RecorrenciaVacina, string> = {
  Nenhuma: '',
  Mensal: 'reforço mensal',
  Trimestral: 'reforço trimestral',
  Semestral: 'reforço semestral',
  Anual: 'reforço anual',
};

/** Mesmos limites da API (CriarVacinaDTO / AtualizarVacinaDTO). */
const MAXIMO_DE_DOSES = 10;
const TAMANHO_NOME = 120;
const TAMANHO_FABRICANTE = 120;
const TAMANHO_LOTE = 60;
const TAMANHO_OBSERVACOES = 500;
const TAMANHO_MINIMO_JUSTIFICATIVA = 5;
const TAMANHO_DA_PAGINA = 10;

const FORM_VAZIO = {
  tipo: 'Vacina' as TipoVacina,
  nome: '',
  fabricante: '',
  lote: '',
  esquema: false,
  numeroDose: '1',
  totalDoses: '3',
  recorrencia: 'Nenhuma' as RecorrenciaVacina,
  dataAplicacao: paraValorInputData(new Date()),
  proximaDose: '',
  veterinarioId: '',
  observacoes: '',
};

type Formulario = typeof FORM_VAZIO;

/**
 * As mesmas regras que a API aplica, conferidas antes de enviar para que o erro
 * apareça na hora e com o campo certo. Datas em "AAAA-MM-DD" se comparam como texto.
 */
function validar(form: Formulario): string | null {
  const nome = form.nome.trim();

  if (nome.length < 2) return 'Informe o nome do produto aplicado (mínimo de 2 caracteres).';
  if (nome.length > TAMANHO_NOME) return `O nome do produto deve ter no máximo ${TAMANHO_NOME} caracteres.`;
  if (form.fabricante.trim().length > TAMANHO_FABRICANTE) {
    return `O fabricante deve ter no máximo ${TAMANHO_FABRICANTE} caracteres.`;
  }
  if (form.lote.trim().length > TAMANHO_LOTE) return `O lote deve ter no máximo ${TAMANHO_LOTE} caracteres.`;
  if (form.observacoes.length > TAMANHO_OBSERVACOES) {
    return `As observações devem ter no máximo ${TAMANHO_OBSERVACOES} caracteres.`;
  }
  if (!form.dataAplicacao) return 'Informe a data da aplicação.';
  if (form.dataAplicacao > paraValorInputData(new Date())) return 'A data de aplicação não pode ser futura.';
  if (form.proximaDose && form.proximaDose <= form.dataAplicacao) {
    return 'A próxima dose deve ser posterior à data de aplicação.';
  }

  if (form.esquema) {
    const numero = Number(form.numeroDose);
    const total = Number(form.totalDoses);

    if (!Number.isInteger(total) || total < 1 || total > MAXIMO_DE_DOSES) {
      return `O esquema deve ter entre 1 e ${MAXIMO_DE_DOSES} doses.`;
    }
    if (!Number.isInteger(numero) || numero < 1 || numero > total) {
      return `O número da dose deve estar entre 1 e ${total}.`;
    }
    if (numero < total && !form.proximaDose && form.recorrencia === 'Nenhuma') {
      return `A dose ${numero} de ${total} exige a data da próxima dose ou uma recorrência para calculá-la.`;
    }
  }

  return null;
}

interface Props {
  pacienteId: string;
  /** Carteira completa vinda do prontuário, usada só para o resumo de pendências. */
  vacinas: Vacina[];
  /** Paciente com óbito registrado: a carteira fica apenas para consulta. */
  somenteLeitura?: boolean;
  aoAtualizar: (mensagem?: string) => void;
}

/**
 * Carteira de vacinação e vermifugação do paciente, com filtro, paginação e controle
 * de vencimento. Esquemas com várias doses são registrados dose a dose; a API confere
 * a sequência e calcula o reforço pela recorrência quando a data não é informada.
 */
export function CarteiraVacinacao({ pacienteId, vacinas, somenteLeitura = false, aoAtualizar }: Props) {
  const { temPerfil } = useAuth();
  const { confirmarEdicao, perguntar } = useConfirmacao();
  // O veterinário responsável registra, corrige e apaga as aplicações dos seus pacientes; a
  // administração, as de todos. A API confere o vínculo com o paciente.
  const podeEditar = temPerfil('Administrador', 'Veterinario') && !somenteLeitura;

  const [busca, setBusca] = useState('');
  const [tipo, setTipo] = useState('');
  const [situacao, setSituacao] = useState('');
  const [numeroPagina, setNumeroPagina] = useState(1);

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<Vacina | null>(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [erroForm, setErroForm] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [removendo, setRemovendo] = useState('');

  const buscar = useCallback(async () => {
    const { data } = await api.get<PaginaDe<Vacina>>(`/api/vacinas/paciente/${pacienteId}`, {
      params: {
        busca: busca || undefined,
        tipo: tipo || undefined,
        situacao: situacao || undefined,
        pagina: numeroPagina,
        tamanho: TAMANHO_DA_PAGINA,
      },
    });

    return data;
  }, [pacienteId, busca, tipo, situacao, numeroPagina]);

  // O atraso evita uma requisição por tecla digitada na busca.
  const { dados, carregando, erro: erroLista, setErro: setErroLista, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar a carteira de vacinação.',
    300,
  );

  // Uma aplicação registrada em outra tela (ou por outro usuário) aparece aqui na hora.
  useAtualizacao(['vacinas'], recarregar);

  const pagina = dados ?? paginaVazia<Vacina>(TAMANHO_DA_PAGINA);
  const filtrosAtivos = Boolean(busca || tipo || situacao);

  // O que exige ação aparece no cabeçalho: vencidas e as que estão por vencer.
  const pendentes = vacinas.filter((v) => v.situacaoDose === 'Vencida' || v.situacaoDose === 'A vencer');

  // Um filtro novo invalida a página atual: o resultado pode ter menos páginas.
  function filtrar(aplicar: () => void) {
    aplicar();
    setNumeroPagina(1);
  }

  function limparFiltros() {
    setBusca('');
    setTipo('');
    setSituacao('');
    setNumeroPagina(1);
  }

  function abrirNovo() {
    setEmEdicao(null);
    setForm(FORM_VAZIO);
    setErroForm('');
    setModalAberto(true);
  }

  /** Lote ou data digitados errado são corrigidos no próprio registro, sem apagar e recriar. */
  function abrirEdicao(vacina: Vacina) {
    setEmEdicao(vacina);
    setForm({
      tipo: vacina.tipo,
      nome: vacina.nome,
      fabricante: vacina.fabricante,
      lote: vacina.lote,
      esquema: vacina.numeroDose != null,
      numeroDose: String(vacina.numeroDose ?? 1),
      totalDoses: String(vacina.totalDoses ?? 3),
      recorrencia: vacina.recorrencia ?? 'Nenhuma',
      dataAplicacao: paraValorInputData(vacina.dataAplicacao),
      proximaDose: vacina.proximaDose ? paraValorInputData(vacina.proximaDose) : '',
      veterinarioId: vacina.veterinarioId ?? '',
      observacoes: vacina.observacoes,
    });
    setErroForm('');
    setModalAberto(true);
  }

  function atualizarForm(parte: Partial<Formulario>) {
    setForm((atual) => {
      const novo = { ...atual, ...parte };

      // O total define o teto do número da dose; a escolha anterior não pode ficar inválida.
      if (Number(novo.numeroDose) > Number(novo.totalDoses)) {
        novo.numeroDose = novo.totalDoses;
      }

      return novo;
    });
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErroForm('');

    const problema = validar(form);

    if (problema) {
      setErroForm(problema);
      return;
    }

    if (
      emEdicao &&
      !(await confirmarEdicao(
        <>
          Salvar a correção da aplicação de <strong>{emEdicao.nome}</strong>? Uma nova data de próxima dose gera
          um novo lembrete para o tutor.
        </>,
        'Salvar correção',
      ))
    ) {
      return;
    }

    setSalvando(true);

    const campos = {
      tipo: form.tipo,
      nome: form.nome.trim(),
      fabricante: form.fabricante.trim(),
      lote: form.lote.trim(),
      numeroDose: form.esquema ? Number(form.numeroDose) : null,
      totalDoses: form.esquema ? Number(form.totalDoses) : null,
      recorrencia: form.recorrencia,
      dataAplicacao: form.dataAplicacao,
      proximaDose: form.proximaDose || null,
      veterinarioId: form.veterinarioId || null,
      observacoes: form.observacoes.trim(),
    };

    try {
      if (emEdicao) {
        await api.put(`/api/vacinas/${emEdicao.id}`, campos);
      } else {
        await api.post('/api/vacinas', { pacienteId, ...campos });
      }

      setModalAberto(false);
      recarregar();
      aoAtualizar(emEdicao ? 'Aplicação corrigida na carteira.' : 'Aplicação registrada na carteira do paciente.');
    } catch (falha) {
      setErroForm(mensagemDeErro(falha, 'Não foi possível salvar a aplicação.'));
    } finally {
      setSalvando(false);
    }
  }

  /** A carteira é um documento do animal: apagar exige dizer por quê, e o motivo fica na auditoria. */
  async function remover(vacina: Vacina) {
    const { confirmado, texto: justificativa } = await perguntar({
      titulo: 'Excluir aplicação',
      mensagem: (
        <>
          Excluir a aplicação de <strong>{vacina.nome}</strong>
          {vacina.descricaoDose && ` (${vacina.descricaoDose.toLowerCase()})`} de {formatarData(vacina.dataAplicacao)}{' '}
          da carteira? O registro some da carteira do paciente e a exclusão fica na auditoria com a
          justificativa.
        </>
      ),
      rotuloConfirmar: 'Excluir',
      rotuloCancelar: 'Voltar',
      perigo: true,
      campoTexto: {
        rotulo: 'Justificativa da exclusão',
        placeholder: 'Ex.: lançada na carteira do paciente errado',
        obrigatorio: true,
      },
    });

    if (!confirmado) {
      return;
    }

    setErro('');

    if (justificativa.length < TAMANHO_MINIMO_JUSTIFICATIVA) {
      setErro(`A justificativa deve ter pelo menos ${TAMANHO_MINIMO_JUSTIFICATIVA} caracteres.`);
      return;
    }

    setRemovendo(vacina.id);

    try {
      await api.delete(`/api/vacinas/${vacina.id}`, { data: { justificativa } });
      recarregar();
      aoAtualizar('Aplicação removida da carteira.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível remover o registro.'));
    } finally {
      setRemovendo('');
    }
  }

  const opcoesDeDose = Array.from({ length: MAXIMO_DE_DOSES }, (_, i) => i + 1);
  const totalDoEsquema = Number(form.totalDoses) || 1;

  return (
    <>
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
          <div>
            <h2 className="font-semibold text-slate-900">Carteira de vacinação</h2>
            {pendentes.length > 0 && (
              <p className="mt-0.5 text-xs text-alerta">
                {pendentes.length} {pendentes.length === 1 ? 'dose exige' : 'doses exigem'} atenção
              </p>
            )}
          </div>

          {podeEditar && (
            <button type="button" className="vc-botao-primario" onClick={abrirNovo}>
              <Plus size={16} />
              Registrar aplicação
            </button>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-3">
          <div className="relative min-w-56 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
            <input
              className="vc-campo pl-10"
              placeholder="Buscar por produto, fabricante ou lote"
              value={busca}
              onChange={(e) => filtrar(() => setBusca(e.target.value))}
              aria-label="Buscar na carteira de vacinação"
            />
          </div>

          <select
            className="vc-campo w-auto"
            value={tipo}
            onChange={(e) => filtrar(() => setTipo(e.target.value))}
            aria-label="Filtrar por tipo"
          >
            <option value="">Todos os tipos</option>
            {TIPOS.map((item) => (
              <option key={item} value={item}>
                {rotuloTipoVacina[item]}
              </option>
            ))}
          </select>

          <select
            className="vc-campo w-auto"
            value={situacao}
            onChange={(e) => filtrar(() => setSituacao(e.target.value))}
            aria-label="Filtrar por situação da dose"
          >
            <option value="">Todas as situações</option>
            {SITUACOES.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>

          {filtrosAtivos && (
            <button type="button" className="vc-botao-sutil" onClick={limparFiltros}>
              Limpar filtros
            </button>
          )}
        </div>

        {(erro || erroLista) && (
          <div className="px-5 pt-4">
            <Alerta
              tipo="erro"
              aoFechar={() => {
                setErro('');
                setErroLista('');
              }}
            >
              {erro || erroLista}
            </Alerta>
          </div>
        )}

        {carregando && !dados ? (
          <Carregando texto="Carregando carteira..." />
        ) : pagina.itens.length === 0 ? (
          <SemDados
            icone={<Syringe size={40} />}
            titulo={filtrosAtivos ? 'Nenhuma aplicação encontrada' : 'Nenhuma aplicação registrada'}
            descricao={
              filtrosAtivos
                ? 'Tente outro termo de busca ou limpe os filtros.'
                : 'Vacinas, vermífugos e antipulgas aplicados ficam registrados aqui, com a data da próxima dose.'
            }
            acao={
              podeEditar && !filtrosAtivos ? (
                <button type="button" className="vc-botao-sutil" onClick={abrirNovo}>
                  <Plus size={16} />
                  Registrar aplicação
                </button>
              ) : undefined
            }
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="vc-tabela">
                <thead>
                  <tr>
                    <th scope="col">Produto</th>
                    <th scope="col">Tipo</th>
                    <th scope="col">Aplicação</th>
                    <th scope="col">Próxima dose</th>
                    <th scope="col">Situação</th>
                    <th scope="col">Aplicada por</th>
                    {podeEditar && (
                      <th scope="col" className="text-right">
                        Ações
                      </th>
                    )}
                  </tr>
                </thead>
                <tbody>
                  {pagina.itens.map((vacina) => (
                    <tr key={vacina.id}>
                      <td>
                        <span className="font-medium text-slate-900">{vacina.nome}</span>
                        {(vacina.descricaoDose || vacina.recorrencia !== 'Nenhuma') && (
                          <span className="block text-xs text-slate-500">
                            {[vacina.descricaoDose, ROTULO_RECORRENCIA[vacina.recorrencia]].filter(Boolean).join(' · ')}
                          </span>
                        )}
                        {vacina.lote && <span className="block text-xs text-slate-400">Lote {vacina.lote}</span>}
                      </td>
                      <td>{rotuloTipoVacina[vacina.tipo] ?? vacina.tipo}</td>
                      <td>{formatarData(vacina.dataAplicacao)}</td>
                      <td>
                        {vacina.proximaDose ? (
                          <>
                            {formatarData(vacina.proximaDose)}
                            {vacina.situacaoDose === 'Concluída' ? (
                              <span className="block text-xs text-slate-400">dose seguinte já aplicada</span>
                            ) : (
                              vacina.diasParaProximaDose != null && (
                                <span className="block text-xs text-slate-400">
                                  {vacina.diasParaProximaDose < 0
                                    ? `${Math.abs(vacina.diasParaProximaDose)} dia(s) em atraso`
                                    : vacina.diasParaProximaDose === 0
                                      ? 'vence hoje'
                                      : `em ${vacina.diasParaProximaDose} dia(s)`}
                                </span>
                              )
                            )}
                          </>
                        ) : (
                          <span className="text-slate-400">—</span>
                        )}
                      </td>
                      <td>
                        <Etiqueta className={estiloSituacaoDose[vacina.situacaoDose]}>{vacina.situacaoDose}</Etiqueta>
                      </td>
                      <td className="text-xs text-slate-500">{vacina.aplicadaPor || '—'}</td>
                      {podeEditar && (
                        <td>
                          <div className="flex justify-end gap-1">
                            <button
                              type="button"
                              onClick={() => abrirEdicao(vacina)}
                              className="rounded-lg p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
                              title="Corrigir registro"
                              aria-label={`Corrigir ${vacina.nome}`}
                            >
                              <Pencil size={15} />
                            </button>

                            <button
                              type="button"
                              onClick={() => remover(vacina)}
                              disabled={removendo === vacina.id}
                              className="rounded-lg p-2 text-slate-400 transition hover:bg-perigo-claro hover:text-perigo disabled:opacity-50"
                              title="Remover registro"
                              aria-label={`Remover ${vacina.nome}`}
                            >
                              {removendo === vacina.id ? <Loader2 className="animate-spin" size={15} /> : <Trash2 size={15} />}
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="aplicações" />
          </>
        )}
      </Card>

      <Modal
        aberto={modalAberto}
        titulo={emEdicao ? 'Corrigir aplicação' : 'Registrar aplicação'}
        descricao="A data da próxima dose gera um lembrete automático para o tutor."
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Tipo" obrigatorio>
              <select
                className="vc-campo"
                value={form.tipo}
                onChange={(e) => atualizarForm({ tipo: e.target.value as TipoVacina })}
              >
                {TIPOS.map((item) => (
                  <option key={item} value={item}>
                    {rotuloTipoVacina[item]}
                  </option>
                ))}
              </select>
            </Campo>

            <Campo rotulo="Produto" obrigatorio>
              <input
                className="vc-campo"
                value={form.nome}
                onChange={(e) => atualizarForm({ nome: e.target.value })}
                placeholder="Ex.: V10, Antirrábica"
                maxLength={TAMANHO_NOME}
                required
              />
            </Campo>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Fabricante">
              <input
                className="vc-campo"
                value={form.fabricante}
                onChange={(e) => atualizarForm({ fabricante: e.target.value })}
                maxLength={TAMANHO_FABRICANTE}
              />
            </Campo>

            <Campo rotulo="Lote">
              <input
                className="vc-campo"
                value={form.lote}
                onChange={(e) => atualizarForm({ lote: e.target.value })}
                maxLength={TAMANHO_LOTE}
              />
            </Campo>
          </div>

          {/* Esquemas com várias doses (V8, V10, giárdia) são registrados dose a dose. */}
          <div className="rounded-xl border border-slate-200 p-3">
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                className="h-4 w-4 rounded accent-brand"
                checked={form.esquema}
                onChange={(e) => atualizarForm({ esquema: e.target.checked })}
              />
              Faz parte de um esquema com várias doses
            </label>

            {form.esquema && (
              <div className="mt-3 grid gap-4 sm:grid-cols-2">
                <Campo rotulo="Total de doses do esquema" obrigatorio>
                  <select
                    className="vc-campo"
                    value={form.totalDoses}
                    onChange={(e) => atualizarForm({ totalDoses: e.target.value })}
                  >
                    {opcoesDeDose.map((n) => (
                      <option key={n} value={n}>
                        {n} {n === 1 ? 'dose' : 'doses'}
                      </option>
                    ))}
                  </select>
                </Campo>

                <Campo rotulo="Número desta dose" obrigatorio dica="A dose 2 só entra depois da dose 1 do mesmo produto.">
                  <select
                    className="vc-campo"
                    value={form.numeroDose}
                    onChange={(e) => atualizarForm({ numeroDose: e.target.value })}
                  >
                    {opcoesDeDose
                      .filter((n) => n <= totalDoEsquema)
                      .map((n) => (
                        <option key={n} value={n}>
                          Dose {n} de {totalDoEsquema}
                        </option>
                      ))}
                  </select>
                </Campo>
              </div>
            )}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Data da aplicação" obrigatorio>
              <input
                type="date"
                className="vc-campo"
                max={paraValorInputData(new Date())}
                value={form.dataAplicacao}
                onChange={(e) => atualizarForm({ dataAplicacao: e.target.value })}
                required
              />
            </Campo>

            <Campo
              rotulo="Próxima dose"
              dica={
                form.recorrencia === 'Nenhuma'
                  ? 'Deixe em branco para dose única.'
                  : 'Deixe em branco para calcular pela recorrência.'
              }
            >
              <input
                type="date"
                className="vc-campo"
                min={form.dataAplicacao}
                value={form.proximaDose}
                onChange={(e) => atualizarForm({ proximaDose: e.target.value })}
              />
            </Campo>
          </div>

          <Campo rotulo="Recorrência do reforço" dica="Antipulgas costuma ser mensal; vacinas, anuais.">
            <select
              className="vc-campo"
              value={form.recorrencia}
              onChange={(e) => atualizarForm({ recorrencia: e.target.value as RecorrenciaVacina })}
            >
              {RECORRENCIAS.map((item) => (
                <option key={item.valor} value={item.valor}>
                  {item.rotulo}
                </option>
              ))}
            </select>
          </Campo>

          <SeletorVeterinario
            valor={form.veterinarioId}
            aoMudar={(veterinarioId) => atualizarForm({ veterinarioId })}
            rotulo="Aplicado por"
            dica={
              emEdicao
                ? 'Deixe em branco para manter quem está registrado.'
                : 'Opcional. Fica registrado na carteira de vacinação.'
            }
          />

          <Campo rotulo="Observações" dica={`${form.observacoes.length}/${TAMANHO_OBSERVACOES} caracteres`}>
            <textarea
              className="vc-campo"
              rows={2}
              value={form.observacoes}
              onChange={(e) => atualizarForm({ observacoes: e.target.value })}
              maxLength={TAMANHO_OBSERVACOES}
            />
          </Campo>

          {erroForm && <Alerta tipo="erro">{erroForm}</Alerta>}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="vc-botao-secundario" onClick={() => setModalAberto(false)} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              {emEdicao ? 'Salvar correção' : 'Registrar'}
            </button>
          </div>
        </form>
      </Modal>
    </>
  );
}
