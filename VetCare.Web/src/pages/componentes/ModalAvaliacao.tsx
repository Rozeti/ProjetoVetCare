import { useState, type FormEvent } from 'react';
import { Loader2, Lock } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { Tratamento } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';

interface Props {
  aberto: boolean;
  tratamentos: Tratamento[];
  aoFechar: () => void;
  aoSalvar: () => void;
}

const FORM_VAZIO = {
  tratamentoId: '',
  queixaPrincipal: '',
  anamnese: '',
  exameFisico: '',
  hipoteseDiagnostica: '',
  planoTerapeutico: '',
  observacaoInterna: '',
};

/** HU-007: registro da avaliação clínica com os cinco campos exigidos pela RN-010. */
export function ModalAvaliacao({ aberto, tratamentos, aoFechar, aoSalvar }: Props) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos
  // novos, o que dispensa um efeito para limpá-los.
  if (!aberto) {
    return null;
  }

  return <Formulario tratamentos={tratamentos} aoFechar={aoFechar} aoSalvar={aoSalvar} />;
}

function Formulario({ tratamentos, aoFechar, aoSalvar }: Omit<Props, 'aberto'>) {
  const { podeVerObservacoesInternas } = useAuth();

  const ativos = tratamentos.filter((t) => t.status === 'Em Andamento');

  // Com um único tratamento em andamento a escolha é óbvia e já vem resolvida.
  const [form, setForm] = useState({
    ...FORM_VAZIO,
    tratamentoId: ativos.length === 1 ? ativos[0].id : '',
  });
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function atualizar(campo: keyof typeof FORM_VAZIO, valor: string) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!form.tratamentoId) {
      setErro('Selecione o tratamento ao qual esta avaliação pertence.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/avaliacoes', {
        tratamentoId: form.tratamentoId,
        queixaPrincipal: form.queixaPrincipal,
        anamnese: form.anamnese,
        exameFisico: form.exameFisico,
        hipoteseDiagnostica: form.hipoteseDiagnostica,
        planoTerapeutico: form.planoTerapeutico,
        observacaoInterna: form.observacaoInterna || null,
      });

      aoSalvar();
    } catch (falha) {
      // RN-010: a API devolve exatamente quais campos obrigatórios faltaram.
      setErro(mensagemDeErro(falha, 'Não foi possível registrar a avaliação.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal
      aberto
      titulo="Registrar avaliação clínica"
      descricao="Todos os cinco campos clínicos são obrigatórios para concluir a avaliação."
      aoFechar={aoFechar}
      largura="max-w-3xl"
    >
      {ativos.length === 0 ? (
        <Alerta tipo="aviso">
          Este paciente não possui tratamento em andamento. Abra um tratamento antes de registrar a avaliação clínica.
        </Alerta>
      ) : (
        <form onSubmit={aoEnviar} className="space-y-4">
          <Campo rotulo="Tratamento" obrigatorio>
            <select
              className="vc-campo"
              value={form.tratamentoId}
              onChange={(e) => atualizar('tratamentoId', e.target.value)}
            >
              <option value="">Selecione o tratamento</option>
              {ativos.map((tratamento) => (
                <option key={tratamento.id} value={tratamento.id}>
                  {tratamento.objetivoTerapeutico.slice(0, 70)}
                </option>
              ))}
            </select>
          </Campo>

          <Campo rotulo="Queixa principal" obrigatorio>
            <textarea
              className="vc-campo"
              rows={2}
              value={form.queixaPrincipal}
              onChange={(e) => atualizar('queixaPrincipal', e.target.value)}
              placeholder="Motivo que trouxe o paciente à clínica."
            />
          </Campo>

          <Campo rotulo="Anamnese" obrigatorio>
            <textarea
              className="vc-campo"
              rows={3}
              value={form.anamnese}
              onChange={(e) => atualizar('anamnese', e.target.value)}
              placeholder="Histórico relatado pelo tutor, cirurgias anteriores, medicações em uso."
            />
          </Campo>

          <Campo rotulo="Exame físico" obrigatorio>
            <textarea
              className="vc-campo"
              rows={3}
              value={form.exameFisico}
              onChange={(e) => atualizar('exameFisico', e.target.value)}
              placeholder="Achados do exame: amplitude de movimento, massa muscular, marcha."
            />
          </Campo>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Hipótese diagnóstica" obrigatorio>
              <textarea
                className="vc-campo"
                rows={3}
                value={form.hipoteseDiagnostica}
                onChange={(e) => atualizar('hipoteseDiagnostica', e.target.value)}
              />
            </Campo>

            <Campo rotulo="Plano terapêutico" obrigatorio>
              <textarea
                className="vc-campo"
                rows={3}
                value={form.planoTerapeutico}
                onChange={(e) => atualizar('planoTerapeutico', e.target.value)}
                placeholder="Técnicas, frequência das sessões e metas."
              />
            </Campo>
          </div>

          {/* HU-009 / RN-003: anotação restrita à equipe clínica. */}
          {podeVerObservacoesInternas && (
            <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
              <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-amber-800">
                <Lock size={15} />
                Observação interna
              </div>
              <textarea
                className="vc-campo"
                rows={2}
                value={form.observacaoInterna}
                onChange={(e) => atualizar('observacaoInterna', e.target.value)}
                placeholder="Anotação que não deve ser exibida ao tutor."
              />
            </div>
          )}

          {erro && <Alerta tipo="erro">{erro}</Alerta>}

          <div className="flex justify-end gap-2 pt-1">
            <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              Registrar avaliação
            </button>
          </div>
        </form>
      )}
    </Modal>
  );
}
