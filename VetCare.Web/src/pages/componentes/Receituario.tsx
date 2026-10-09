import { useState, type FormEvent } from 'react';
import { Ban, Loader2, Pill, Plus, Printer, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { Prescricao } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { SeletorVeterinario } from '../../components/SeletorVeterinario';
import { formatarData, formatarDia, paraValorInputData } from '../../utils/formato';
import { imprimirReceita } from '../../utils/impressao';

const VIAS = ['Oral', 'Tópica', 'Intramuscular', 'Subcutânea', 'Intravenosa', 'Oftálmica', 'Otológica'];

/** Mesmos limites de CriarPrescricaoDTO e da validade aceita pela API. */
const LIMITES = {
  medicamento: 200,
  dosagem: 120,
  frequencia: 120,
  duracao: 120,
  observacao: 500,
  orientacoes: 2000,
};

const VALIDADE_MAXIMA_EM_DIAS = 365;

function validadeMaxima(): Date {
  const data = new Date();
  data.setDate(data.getDate() + VALIDADE_MAXIMA_EM_DIAS);
  return data;
}

interface ItemFormulario {
  medicamento: string;
  dosagem: string;
  frequencia: string;
  duracao: string;
  via: string;
  observacao: string;
}

const ITEM_VAZIO: ItemFormulario = {
  medicamento: '',
  dosagem: '',
  frequencia: '',
  duracao: '',
  via: 'Oral',
  observacao: '',
};

interface Props {
  pacienteId: string;
  prescricoes: Prescricao[];
  nomeClinica: string;
  /** Recarrega o prontuário; a mensagem, quando vem, é exibida pela tela que o contém. */
  aoAtualizar: (mensagem?: string) => void;
}

/** Receituário do paciente, com emissão, cancelamento e impressão. */
export function Receituario({ prescricoes, pacienteId, nomeClinica, aoAtualizar }: Props) {
  const { temPerfil, ehVeterinario } = useAuth();
  const podeEmitir = temPerfil('Administrador', 'Veterinario');

  const [modalAberto, setModalAberto] = useState(false);
  const [itens, setItens] = useState<ItemFormulario[]>([{ ...ITEM_VAZIO }]);
  // O veterinário logado assina a própria receita; o administrador precisa escolher quem assina.
  const [veterinarioId, setVeterinarioId] = useState('');
  const [orientacoes, setOrientacoes] = useState('');
  const [validaAte, setValidaAte] = useState(paraValorInputData(proximosTrintaDias()));
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function abrirNova() {
    setItens([{ ...ITEM_VAZIO }]);
    setVeterinarioId('');
    setOrientacoes('');
    setValidaAte(paraValorInputData(proximosTrintaDias()));
    setErro('');
    setModalAberto(true);
  }

  function atualizarItem(indice: number, campo: keyof ItemFormulario, valor: string) {
    setItens((atual) => atual.map((item, i) => (i === indice ? { ...item, [campo]: valor } : item)));
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    const preenchidos = itens.filter((i) => i.medicamento.trim());

    if (preenchidos.length === 0) {
      setErro('Informe ao menos um medicamento.');
      return;
    }

    const semDosagem = preenchidos.find((i) => !i.dosagem.trim() || !i.frequencia.trim());

    if (semDosagem) {
      setErro(`Informe a dosagem e a frequência de ${semDosagem.medicamento}.`);
      return;
    }

    // Mesmos limites de CriarPrescricaoDTO: o erro aparece antes de a receita ir para a API.
    const itemInvalido = preenchidos.find(
      (i) =>
        i.medicamento.trim().length < 2 ||
        i.medicamento.trim().length > LIMITES.medicamento ||
        i.dosagem.trim().length > LIMITES.dosagem ||
        i.frequencia.trim().length > LIMITES.frequencia ||
        i.duracao.trim().length > LIMITES.duracao ||
        i.observacao.trim().length > LIMITES.observacao,
    );

    if (itemInvalido) {
      setErro(
        `Confira o item ${itemInvalido.medicamento || 'sem nome'}: medicamento com 2 a ${LIMITES.medicamento} caracteres, ` +
          `dosagem, frequência e duração com até ${LIMITES.dosagem}, observação com até ${LIMITES.observacao}.`,
      );
      return;
    }

    if (orientacoes.length > LIMITES.orientacoes) {
      setErro(`As orientações devem ter até ${LIMITES.orientacoes} caracteres.`);
      return;
    }

    const hoje = paraValorInputData(new Date());

    if (validaAte && validaAte <= hoje) {
      setErro('A validade da receita deve ser uma data futura.');
      return;
    }

    if (validaAte && validaAte > paraValorInputData(validadeMaxima())) {
      setErro(`A validade da receita não pode passar de ${VALIDADE_MAXIMA_EM_DIAS} dias.`);
      return;
    }

    if (!ehVeterinario && !veterinarioId) {
      setErro('Selecione o veterinário responsável pela receita.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/prescricoes', {
        pacienteId,
        veterinarioId: veterinarioId || null,
        validaAte: validaAte || null,
        orientacoes: orientacoes.trim(),
        itens: preenchidos.map((i) => ({
          medicamento: i.medicamento.trim(),
          dosagem: i.dosagem.trim(),
          frequencia: i.frequencia.trim(),
          duracao: i.duracao.trim(),
          via: i.via,
          observacao: i.observacao.trim(),
        })),
      });

      setModalAberto(false);
      aoAtualizar('Receita emitida com sucesso.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível emitir a receita.'));
    } finally {
      setSalvando(false);
    }
  }

  const { perguntar } = useConfirmacao();

  // Limite inferior do campo de validade, calculado uma vez: o lint pede funções puras na renderização.
  const [amanha] = useState(() => paraValorInputData(new Date(Date.now() + 86_400_000)));

  const [cancelando, setCancelando] = useState('');

  async function cancelar(prescricao: Prescricao) {
    const { confirmado, texto: motivo } = await perguntar({
      titulo: 'Cancelar receita',
      mensagem: (
        <>
          Cancelar a receita emitida em <strong>{formatarData(prescricao.dataEmissao)}</strong>? Ela continua no
          prontuário como cancelada e, se for impressa, sai marcada como sem validade.
        </>
      ),
      rotuloConfirmar: 'Cancelar receita',
      rotuloCancelar: 'Voltar',
      perigo: true,
      campoTexto: { rotulo: 'Motivo do cancelamento', placeholder: 'Ex.: dosagem revista', obrigatorio: true },
    });

    if (!confirmado) {
      return;
    }

    if (motivo.length > 300) {
      setErro('O motivo do cancelamento deve ter até 300 caracteres.');
      return;
    }

    setErro('');
    setCancelando(prescricao.id);

    try {
      await api.patch(`/api/prescricoes/${prescricao.id}/cancelar`, { motivo });
      aoAtualizar('Receita cancelada. O registro permanece no histórico do paciente.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível cancelar a receita.'));
    } finally {
      setCancelando('');
    }
  }

  return (
    <>
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-900">Receituário</h2>

          {podeEmitir && (
            <button type="button" className="vc-botao-primario" onClick={abrirNova}>
              <Plus size={16} />
              Nova receita
            </button>
          )}
        </div>

        {erro && (
          <div className="px-5 pt-4">
            <Alerta tipo="erro" aoFechar={() => setErro('')}>
              {erro}
            </Alerta>
          </div>
        )}

        {prescricoes.length === 0 ? (
          <SemDados
            icone={<Pill size={40} />}
            titulo="Nenhuma receita emitida"
            descricao="As receitas emitidas ficam disponíveis aqui para consulta e impressão."
          />
        ) : (
          <ul className="divide-y divide-slate-100">
            {prescricoes.map((prescricao) => (
              <li key={prescricao.id} className="px-5 py-4">
                <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-slate-900">
                        Receita de {formatarData(prescricao.dataEmissao)}
                      </span>

                      <Etiqueta
                        className={
                          prescricao.status === 'Ativa'
                            ? 'bg-sucesso-claro text-emerald-800'
                            : 'bg-slate-200 text-slate-600'
                        }
                      >
                        {prescricao.status}
                      </Etiqueta>
                    </div>

                    <p className="mt-0.5 text-xs text-slate-500">
                      {prescricao.nomeVeterinario}
                      {prescricao.crmv && ` · ${prescricao.crmv}`}
                      {prescricao.validaAte && ` · Válida até ${formatarDia(prescricao.validaAte)}`}
                    </p>
                  </div>

                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => imprimirReceita(prescricao, nomeClinica)}
                      className="rounded-lg p-2 text-slate-500 transition hover:bg-brand-100 hover:text-brand"
                      title="Imprimir receita"
                      aria-label={`Imprimir a receita de ${formatarData(prescricao.dataEmissao)}`}
                    >
                      <Printer size={16} />
                    </button>

                    {podeEmitir && prescricao.status === 'Ativa' && (
                      <button
                        type="button"
                        onClick={() => cancelar(prescricao)}
                        disabled={cancelando === prescricao.id}
                        className="rounded-lg p-2 text-slate-500 transition hover:bg-perigo-claro hover:text-perigo disabled:opacity-50"
                        title="Cancelar receita"
                        aria-label={`Cancelar a receita de ${formatarData(prescricao.dataEmissao)}`}
                      >
                        {cancelando === prescricao.id ? <Loader2 className="animate-spin" size={16} /> : <Ban size={16} />}
                      </button>
                    )}
                  </div>
                </div>

                <ul className="space-y-2">
                  {prescricao.itens.map((item) => (
                    <li key={item.id} className="rounded-xl bg-slate-50 px-4 py-3">
                      <p className="text-sm font-semibold text-slate-800">{item.medicamento}</p>
                      <p className="mt-0.5 text-xs text-slate-600">
                        {[item.dosagem, item.frequencia, item.duracao, item.via].filter(Boolean).join(' · ')}
                      </p>
                      {item.observacao && <p className="mt-1 text-xs text-slate-500">{item.observacao}</p>}
                    </li>
                  ))}
                </ul>

                {prescricao.orientacoes && (
                  <p className="mt-3 text-sm text-slate-600">
                    <strong className="text-slate-700">Orientações:</strong> {prescricao.orientacoes}
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Modal
        aberto={modalAberto}
        titulo="Nova receita"
        descricao="A receita fica registrada no prontuário e pode ser impressa para o tutor."
        aoFechar={() => setModalAberto(false)}
        largura="max-w-3xl"
      >
        <form onSubmit={aoEnviar} className="space-y-4" noValidate>
          {itens.map((item, indice) => (
            <fieldset key={indice} className="rounded-xl border border-slate-200 p-4">
              <legend className="flex items-center gap-2 px-2 text-sm font-semibold text-slate-700">
                Medicamento {indice + 1}
                {itens.length > 1 && (
                  <button
                    type="button"
                    onClick={() => setItens((atual) => atual.filter((_, i) => i !== indice))}
                    className="text-slate-400 transition hover:text-perigo"
                    aria-label={`Remover medicamento ${indice + 1}`}
                  >
                    <Trash2 size={14} />
                  </button>
                )}
              </legend>

              <div className="space-y-3">
                <Campo rotulo="Medicamento" obrigatorio>
                  <input
                    className="vc-campo"
                    value={item.medicamento}
                    onChange={(e) => atualizarItem(indice, 'medicamento', e.target.value)}
                    placeholder="Nome e concentração"
                    maxLength={LIMITES.medicamento}
                  />
                </Campo>

                <div className="grid gap-3 sm:grid-cols-2">
                  <Campo rotulo="Dosagem" obrigatorio>
                    <input
                      className="vc-campo"
                      value={item.dosagem}
                      onChange={(e) => atualizarItem(indice, 'dosagem', e.target.value)}
                      placeholder="Ex.: 1 comprimido"
                      maxLength={LIMITES.dosagem}
                    />
                  </Campo>

                  <Campo rotulo="Frequência" obrigatorio>
                    <input
                      className="vc-campo"
                      value={item.frequencia}
                      onChange={(e) => atualizarItem(indice, 'frequencia', e.target.value)}
                      placeholder="Ex.: a cada 12 horas"
                      maxLength={LIMITES.frequencia}
                    />
                  </Campo>

                  <Campo rotulo="Duração">
                    <input
                      className="vc-campo"
                      value={item.duracao}
                      onChange={(e) => atualizarItem(indice, 'duracao', e.target.value)}
                      placeholder="Ex.: por 7 dias"
                      maxLength={LIMITES.duracao}
                    />
                  </Campo>

                  <Campo rotulo="Via">
                    <select
                      className="vc-campo"
                      value={item.via}
                      onChange={(e) => atualizarItem(indice, 'via', e.target.value)}
                    >
                      {VIAS.map((via) => (
                        <option key={via}>{via}</option>
                      ))}
                    </select>
                  </Campo>
                </div>

                <Campo rotulo="Observação">
                  <input
                    className="vc-campo"
                    value={item.observacao}
                    onChange={(e) => atualizarItem(indice, 'observacao', e.target.value)}
                    placeholder="Ex.: administrar junto com alimento"
                    maxLength={LIMITES.observacao}
                  />
                </Campo>
              </div>
            </fieldset>
          ))}

          <button
            type="button"
            className="vc-botao-secundario w-full"
            onClick={() => setItens((atual) => [...atual, { ...ITEM_VAZIO }])}
          >
            <Plus size={16} />
            Adicionar medicamento
          </button>

          <div className="grid gap-4 sm:grid-cols-2">
            <SeletorVeterinario valor={veterinarioId} aoMudar={setVeterinarioId} obrigatorio />

            <Campo rotulo="Válida até" dica={`Data futura, até ${VALIDADE_MAXIMA_EM_DIAS} dias. Em branco, 30 dias.`}>
              <input
                type="date"
                className="vc-campo"
                min={amanha}
                max={paraValorInputData(validadeMaxima())}
                value={validaAte}
                onChange={(e) => setValidaAte(e.target.value)}
              />
            </Campo>
          </div>

          <Campo rotulo="Orientações gerais" dica={`${orientacoes.length}/${LIMITES.orientacoes} caracteres`}>
            <textarea
              className="vc-campo"
              rows={3}
              value={orientacoes}
              onChange={(e) => setOrientacoes(e.target.value)}
              placeholder="Cuidados, sinais de alerta e quando retornar à clínica."
              maxLength={LIMITES.orientacoes}
            />
          </Campo>

          {erro && <Alerta tipo="erro">{erro}</Alerta>}

          <div className="flex justify-end gap-2 pt-1">
            <button type="button" className="vc-botao-secundario" onClick={() => setModalAberto(false)} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              Emitir receita
            </button>
          </div>
        </form>
      </Modal>
    </>
  );
}

function proximosTrintaDias(): Date {
  const data = new Date();
  data.setDate(data.getDate() + 30);
  return data;
}
