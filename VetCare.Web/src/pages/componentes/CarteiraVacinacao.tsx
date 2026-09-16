import { useState, type FormEvent } from 'react';
import { Loader2, Plus, Syringe, Trash2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { SituacaoDose, Vacina } from '../../types';
import { Alerta, Campo, Card, Etiqueta, Modal, SemDados } from '../../components/ui';
import { SeletorVeterinario } from '../../components/SeletorVeterinario';
import { formatarData, paraValorInputData } from '../../utils/formato';

const ESTILO_SITUACAO: Record<SituacaoDose, string> = {
  Vencida: 'bg-perigo-claro text-red-800',
  'A vencer': 'bg-alerta-claro text-amber-800',
  'Em dia': 'bg-sucesso-claro text-emerald-800',
  'Dose única': 'bg-slate-100 text-slate-600',
};

const TIPOS = ['Vacina', 'Vermifugo', 'Antipulgas', 'Outro'] as const;

const ROTULO_TIPO: Record<string, string> = {
  Vacina: 'Vacina',
  Vermifugo: 'Vermífugo',
  Antipulgas: 'Antipulgas',
  Outro: 'Outro',
};

const FORM_VAZIO = {
  tipo: 'Vacina',
  nome: '',
  fabricante: '',
  lote: '',
  dataAplicacao: paraValorInputData(new Date()),
  proximaDose: '',
  observacoes: '',
};

interface Props {
  pacienteId: string;
  vacinas: Vacina[];
  aoAtualizar: () => void;
}

/** Carteira de vacinação e vermifugação do paciente, com controle de vencimento. */
export function CarteiraVacinacao({ pacienteId, vacinas, aoAtualizar }: Props) {
  const { temPerfil } = useAuth();
  const podeEditar = temPerfil('Administrador', 'Veterinario');
  const podeRemover = temPerfil('Administrador');

  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState(FORM_VAZIO);
  // Quem aplicou a dose fica registrado na carteira; o veterinário logado assina sozinho.
  const [veterinarioId, setVeterinarioId] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function abrirNovo() {
    setForm(FORM_VAZIO);
    setVeterinarioId('');
    setErro('');
    setModalAberto(true);
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!form.nome.trim()) {
      setErro('Informe o nome do produto aplicado.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/vacinas', {
        pacienteId,
        veterinarioId: veterinarioId || null,
        tipo: form.tipo,
        nome: form.nome,
        fabricante: form.fabricante,
        lote: form.lote,
        dataAplicacao: form.dataAplicacao,
        proximaDose: form.proximaDose || null,
        observacoes: form.observacoes,
      });

      setModalAberto(false);
      aoAtualizar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível registrar a aplicação.'));
    } finally {
      setSalvando(false);
    }
  }

  async function remover(vacina: Vacina) {
    if (!window.confirm(`Remover o registro de ${vacina.nome} da carteira?`)) {
      return;
    }

    try {
      await api.delete(`/api/vacinas/${vacina.id}`);
      aoAtualizar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível remover o registro.'));
    }
  }

  // O que exige ação aparece primeiro: vencidas, depois as que estão por vencer.
  const pendentes = vacinas.filter((v) => v.situacaoDose === 'Vencida' || v.situacaoDose === 'A vencer');

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

        {erro && (
          <div className="px-5 pt-4">
            <Alerta tipo="erro" aoFechar={() => setErro('')}>
              {erro}
            </Alerta>
          </div>
        )}

        {vacinas.length === 0 ? (
          <SemDados
            icone={<Syringe size={40} />}
            titulo="Nenhuma aplicação registrada"
            descricao="Vacinas, vermífugos e antipulgas aplicados ficam registrados aqui, com a data da próxima dose."
            acao={
              podeEditar ? (
                <button type="button" className="vc-botao-sutil" onClick={abrirNovo}>
                  <Plus size={16} />
                  Registrar aplicação
                </button>
              ) : undefined
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th>Produto</th>
                  <th>Tipo</th>
                  <th>Aplicação</th>
                  <th>Próxima dose</th>
                  <th>Situação</th>
                  <th>Aplicada por</th>
                  {podeRemover && <th className="text-right">Ações</th>}
                </tr>
              </thead>
              <tbody>
                {vacinas.map((vacina) => (
                  <tr key={vacina.id}>
                    <td>
                      <span className="font-medium text-slate-900">{vacina.nome}</span>
                      {vacina.lote && <span className="block text-xs text-slate-400">Lote {vacina.lote}</span>}
                    </td>
                    <td>{ROTULO_TIPO[vacina.tipo] ?? vacina.tipo}</td>
                    <td>{formatarData(vacina.dataAplicacao)}</td>
                    <td>
                      {vacina.proximaDose ? (
                        <>
                          {formatarData(vacina.proximaDose)}
                          {vacina.diasParaProximaDose != null && (
                            <span className="block text-xs text-slate-400">
                              {vacina.diasParaProximaDose < 0
                                ? `${Math.abs(vacina.diasParaProximaDose)} dia(s) em atraso`
                                : `em ${vacina.diasParaProximaDose} dia(s)`}
                            </span>
                          )}
                        </>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                    <td>
                      <Etiqueta className={ESTILO_SITUACAO[vacina.situacaoDose]}>{vacina.situacaoDose}</Etiqueta>
                    </td>
                    <td className="text-xs text-slate-500">{vacina.aplicadaPor || '—'}</td>
                    {podeRemover && (
                      <td>
                        <div className="flex justify-end">
                          <button
                            type="button"
                            onClick={() => remover(vacina)}
                            className="rounded-lg p-2 text-slate-400 transition hover:bg-perigo-claro hover:text-perigo"
                            title="Remover registro"
                          >
                            <Trash2 size={15} />
                          </button>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Modal
        aberto={modalAberto}
        titulo="Registrar aplicação"
        descricao="A data da próxima dose gera um lembrete automático para o tutor."
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Tipo" obrigatorio>
              <select className="vc-campo" value={form.tipo} onChange={(e) => setForm({ ...form, tipo: e.target.value })}>
                {TIPOS.map((tipo) => (
                  <option key={tipo} value={tipo}>
                    {ROTULO_TIPO[tipo]}
                  </option>
                ))}
              </select>
            </Campo>

            <Campo rotulo="Produto" obrigatorio>
              <input
                className="vc-campo"
                value={form.nome}
                onChange={(e) => setForm({ ...form, nome: e.target.value })}
                placeholder="Ex.: V10, Antirrábica"
              />
            </Campo>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Fabricante">
              <input
                className="vc-campo"
                value={form.fabricante}
                onChange={(e) => setForm({ ...form, fabricante: e.target.value })}
              />
            </Campo>

            <Campo rotulo="Lote">
              <input className="vc-campo" value={form.lote} onChange={(e) => setForm({ ...form, lote: e.target.value })} />
            </Campo>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Data da aplicação" obrigatorio>
              <input
                type="date"
                className="vc-campo"
                max={paraValorInputData(new Date())}
                value={form.dataAplicacao}
                onChange={(e) => setForm({ ...form, dataAplicacao: e.target.value })}
              />
            </Campo>

            <Campo rotulo="Próxima dose" dica="Deixe em branco para dose única.">
              <input
                type="date"
                className="vc-campo"
                min={form.dataAplicacao}
                value={form.proximaDose}
                onChange={(e) => setForm({ ...form, proximaDose: e.target.value })}
              />
            </Campo>
          </div>

          <SeletorVeterinario
            valor={veterinarioId}
            aoMudar={setVeterinarioId}
            rotulo="Aplicado por"
            dica="Opcional. Fica registrado na carteira de vacinação."
          />

          <Campo rotulo="Observações">
            <textarea
              className="vc-campo"
              rows={2}
              value={form.observacoes}
              onChange={(e) => setForm({ ...form, observacoes: e.target.value })}
            />
          </Campo>

          {erro && <Alerta tipo="erro">{erro}</Alerta>}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="vc-botao-secundario" onClick={() => setModalAberto(false)} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              Registrar
            </button>
          </div>
        </form>
      </Modal>
    </>
  );
}
