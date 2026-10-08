import { useState, type FormEvent } from 'react';
import { Loader2, Lock } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import { useAuth } from '../../contexts/auth';
import type { Avaliacao, Tratamento } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';

interface Props {
  aberto: boolean;
  tratamentos: Tratamento[];
  /** Quando informada, o modal corrige esta avaliação em vez de registrar uma nova (RN-004). */
  avaliacao?: Avaliacao | null;
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

type Campos = keyof typeof FORM_VAZIO;

/** Mesmos limites das colunas do prontuário (CriarAvaliacaoDTO). */
const LIMITES: Record<Exclude<Campos, 'tratamentoId'>, number> = {
  queixaPrincipal: 2000,
  anamnese: 4000,
  exameFisico: 4000,
  hipoteseDiagnostica: 2000,
  planoTerapeutico: 4000,
  observacaoInterna: 2000,
};

/** RN-010: os cinco campos clínicos, na ordem em que aparecem na tela. */
const CAMPOS_CLINICOS: { campo: Exclude<Campos, 'tratamentoId' | 'observacaoInterna'>; rotulo: string }[] = [
  { campo: 'queixaPrincipal', rotulo: 'a queixa principal' },
  { campo: 'anamnese', rotulo: 'a anamnese' },
  { campo: 'exameFisico', rotulo: 'o exame físico' },
  { campo: 'hipoteseDiagnostica', rotulo: 'a hipótese diagnóstica' },
  { campo: 'planoTerapeutico', rotulo: 'o plano terapêutico' },
];

/** HU-007: registro e correção da avaliação clínica com os cinco campos exigidos pela RN-010. */
export function ModalAvaliacao({ aberto, ...props }: Props) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos
  // novos, o que dispensa um efeito para limpá-los.
  if (!aberto) {
    return null;
  }

  return <Formulario {...props} />;
}

function Formulario({ tratamentos, avaliacao, aoFechar, aoSalvar }: Omit<Props, 'aberto'>) {
  const { podeVerObservacoesInternas } = useAuth();

  const emEdicao = !!avaliacao;
  const ativos = tratamentos.filter((t) => t.status === 'Em Andamento');

  // Com um único tratamento em andamento a escolha é óbvia e já vem resolvida.
  const [form, setForm] = useState(
    avaliacao
      ? {
          ...FORM_VAZIO,
          tratamentoId: avaliacao.tratamentoId,
          queixaPrincipal: avaliacao.queixaPrincipal,
          anamnese: avaliacao.anamnese,
          exameFisico: avaliacao.exameFisico,
          hipoteseDiagnostica: avaliacao.hipoteseDiagnostica,
          planoTerapeutico: avaliacao.planoTerapeutico,
        }
      : { ...FORM_VAZIO, tratamentoId: ativos.length === 1 ? ativos[0].id : '' },
  );
  const [erros, setErros] = useState<Partial<Record<Campos, string>>>({});
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function atualizar(campo: Campos, valor: string) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
    // O erro do campo some assim que ele é editado; o usuário vê o que ainda falta.
    setErros((atuais) => (atuais[campo] ? { ...atuais, [campo]: undefined } : atuais));
  }

  /** RN-010 conferida antes de enviar, com o erro ao lado do campo que falta. */
  function validar(): boolean {
    const encontrados: Partial<Record<Campos, string>> = {};

    if (!emEdicao && !form.tratamentoId) {
      encontrados.tratamentoId = 'Selecione o tratamento ao qual esta avaliação pertence.';
    }

    for (const { campo, rotulo } of CAMPOS_CLINICOS) {
      const valor = form[campo].trim();

      if (!valor) {
        encontrados[campo] = `Preencha ${rotulo}.`;
      } else if (valor.length > LIMITES[campo]) {
        encontrados[campo] = `Use no máximo ${LIMITES[campo]} caracteres.`;
      }
    }

    if (form.observacaoInterna.length > LIMITES.observacaoInterna) {
      encontrados.observacaoInterna = `Use no máximo ${LIMITES.observacaoInterna} caracteres.`;
    }

    setErros(encontrados);

    const faltando = CAMPOS_CLINICOS.filter(({ campo }) => encontrados[campo]).map(({ rotulo }) => rotulo);

    if (faltando.length > 0) {
      setErro(`Campos obrigatórios pendentes: ${faltando.join(', ')}.`);
    }

    return Object.keys(encontrados).length === 0;
  }

  const { confirmarEdicao } = useConfirmacao();

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!validar()) {
      return;
    }

    if (
      avaliacao &&
      !(await confirmarEdicao(
        'Salvar a correção desta avaliação? A versão anterior fica preservada no histórico do prontuário.',
        'Salvar correção',
      ))
    ) {
      return;
    }

    setSalvando(true);

    const campos = {
      queixaPrincipal: form.queixaPrincipal.trim(),
      anamnese: form.anamnese.trim(),
      exameFisico: form.exameFisico.trim(),
      hipoteseDiagnostica: form.hipoteseDiagnostica.trim(),
      planoTerapeutico: form.planoTerapeutico.trim(),
    };

    try {
      if (avaliacao) {
        // RN-004: a API arquiva a versão anterior antes de aplicar a correção.
        await api.put(`/api/avaliacoes/${avaliacao.id}`, campos);
      } else {
        await api.post('/api/avaliacoes', {
          tratamentoId: form.tratamentoId,
          ...campos,
          observacaoInterna: form.observacaoInterna.trim() || null,
        });
      }

      aoSalvar();
    } catch (falha) {
      // RN-010: a API devolve exatamente quais campos obrigatórios faltaram.
      setErro(mensagemDeErro(falha, 'Não foi possível salvar a avaliação.'));
    } finally {
      setSalvando(false);
    }
  }

  function contador(campo: Exclude<Campos, 'tratamentoId'>) {
    return `${form[campo].length}/${LIMITES[campo]}`;
  }

  return (
    <Modal
      aberto
      titulo={emEdicao ? 'Corrigir avaliação clínica' : 'Registrar avaliação clínica'}
      descricao={
        emEdicao
          ? 'A versão anterior fica guardada no histórico do prontuário (RN-004).'
          : 'Todos os cinco campos clínicos são obrigatórios para concluir a avaliação.'
      }
      aoFechar={aoFechar}
      largura="max-w-3xl"
    >
      {!emEdicao && ativos.length === 0 ? (
        <Alerta tipo="aviso">
          Este paciente não possui tratamento em andamento. Abra um tratamento antes de registrar a avaliação clínica.
        </Alerta>
      ) : (
        <form onSubmit={aoEnviar} className="space-y-4" noValidate>
          {!emEdicao && (
            <Campo rotulo="Tratamento" obrigatorio erro={erros.tratamentoId}>
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
          )}

          <Campo rotulo="Queixa principal" obrigatorio erro={erros.queixaPrincipal} dica={contador('queixaPrincipal')}>
            <textarea
              className="vc-campo"
              rows={2}
              value={form.queixaPrincipal}
              onChange={(e) => atualizar('queixaPrincipal', e.target.value)}
              placeholder="Motivo que trouxe o paciente à clínica."
              maxLength={LIMITES.queixaPrincipal}
            />
          </Campo>

          <Campo rotulo="Anamnese" obrigatorio erro={erros.anamnese} dica={contador('anamnese')}>
            <textarea
              className="vc-campo"
              rows={3}
              value={form.anamnese}
              onChange={(e) => atualizar('anamnese', e.target.value)}
              placeholder="Histórico relatado pelo tutor, cirurgias anteriores, medicações em uso."
              maxLength={LIMITES.anamnese}
            />
          </Campo>

          <Campo rotulo="Exame físico" obrigatorio erro={erros.exameFisico} dica={contador('exameFisico')}>
            <textarea
              className="vc-campo"
              rows={3}
              value={form.exameFisico}
              onChange={(e) => atualizar('exameFisico', e.target.value)}
              placeholder="Achados do exame: amplitude de movimento, massa muscular, marcha."
              maxLength={LIMITES.exameFisico}
            />
          </Campo>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo
              rotulo="Hipótese diagnóstica"
              obrigatorio
              erro={erros.hipoteseDiagnostica}
              dica={contador('hipoteseDiagnostica')}
            >
              <textarea
                className="vc-campo"
                rows={3}
                value={form.hipoteseDiagnostica}
                onChange={(e) => atualizar('hipoteseDiagnostica', e.target.value)}
                maxLength={LIMITES.hipoteseDiagnostica}
              />
            </Campo>

            <Campo rotulo="Plano terapêutico" obrigatorio erro={erros.planoTerapeutico} dica={contador('planoTerapeutico')}>
              <textarea
                className="vc-campo"
                rows={3}
                value={form.planoTerapeutico}
                onChange={(e) => atualizar('planoTerapeutico', e.target.value)}
                placeholder="Técnicas, frequência das sessões e metas."
                maxLength={LIMITES.planoTerapeutico}
              />
            </Campo>
          </div>

          {/* HU-009 / RN-003: anotação restrita à equipe clínica, só no registro inicial. */}
          {!emEdicao && podeVerObservacoesInternas && (
            <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
              <label className="mb-2 flex items-center gap-2 text-sm font-semibold text-amber-800" htmlFor="avaliacao-observacao-interna">
                <Lock size={15} />
                Observação interna
                <span className="ml-auto text-xs font-normal text-amber-700">{contador('observacaoInterna')}</span>
              </label>
              <textarea
                id="avaliacao-observacao-interna"
                className="vc-campo"
                rows={2}
                value={form.observacaoInterna}
                onChange={(e) => atualizar('observacaoInterna', e.target.value)}
                placeholder="Anotação que não deve ser exibida ao tutor."
                maxLength={LIMITES.observacaoInterna}
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
              {emEdicao ? 'Salvar correção' : 'Registrar avaliação'}
            </button>
          </div>
        </form>
      )}
    </Modal>
  );
}
