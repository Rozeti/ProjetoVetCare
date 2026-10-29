import { useState, type FormEvent } from 'react';
import { Loader2, Lock } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import { useAuth } from '../../contexts/auth';
import type { Avaliacao } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { SeletorVeterinario } from '../../components/SeletorVeterinario';

interface Props {
  aberto: boolean;
  pacienteId: string;
  /** Veterinário que acompanha o paciente, sugerido para assinar. Nulo quando ainda não há responsável. */
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel?: string;
  /** Quando informada, o modal corrige esta avaliação em vez de registrar uma nova (RN-004). */
  avaliacao?: Avaliacao | null;
  aoFechar: () => void;
  aoSalvar: () => void;
}

const FORM_VAZIO = {
  motivoConsulta: '',
  observacoes: '',
  diagnosticoPossivel: '',
  observacaoInterna: '',
};

type Campos = keyof typeof FORM_VAZIO;

/** Mesmos limites de CriarAvaliacaoDTO; a correção aceita observações mais longas. */
const LIMITES: Record<Campos, number> = {
  motivoConsulta: 2000,
  observacoes: 4000,
  diagnosticoPossivel: 2000,
  observacaoInterna: 2000,
};

/** RN-010: os campos clínicos obrigatórios, na ordem em que aparecem na tela. */
const CAMPOS_CLINICOS: { campo: Exclude<Campos, 'observacaoInterna'>; rotulo: string }[] = [
  { campo: 'motivoConsulta', rotulo: 'o motivo da consulta' },
  { campo: 'observacoes', rotulo: 'as observações' },
  { campo: 'diagnosticoPossivel', rotulo: 'o possível diagnóstico' },
];

/**
 * HU-007: a avaliação é o primeiro registro clínico de uma consulta. Ela pertence ao animal
 * e, depois de registrada, pode originar tratamentos, receitas e pedidos de exame.
 */
export function ModalAvaliacao({ aberto, ...props }: Props) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos
  // novos, o que dispensa um efeito para limpá-los.
  if (!aberto) {
    return null;
  }

  return <Formulario {...props} />;
}

function Formulario({
  pacienteId,
  veterinarioResponsavelId,
  nomeVeterinarioResponsavel,
  avaliacao,
  aoFechar,
  aoSalvar,
}: Omit<Props, 'aberto'>) {
  const { podeVerObservacoesInternas, ehVeterinario } = useAuth();

  const emEdicao = !!avaliacao;
  // O veterinário assina a própria avaliação; administração e apoio escolhem quem assina, com o
  // responsável pelo paciente já sugerido. Sem responsável, quem assina passa a acompanhá-lo.
  const escolheVeterinario = !emEdicao && !ehVeterinario;

  const [form, setForm] = useState(
    avaliacao
      ? {
          ...FORM_VAZIO,
          motivoConsulta: avaliacao.motivoConsulta,
          observacoes: avaliacao.observacoes,
          diagnosticoPossivel: avaliacao.diagnosticoPossivel,
        }
      : FORM_VAZIO,
  );
  const [veterinarioId, setVeterinarioId] = useState(veterinarioResponsavelId ?? '');
  const [erros, setErros] = useState<Partial<Record<Campos, string>>>({});
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const limiteObservacoes = emEdicao ? 8000 : LIMITES.observacoes;

  function atualizar(campo: Campos, valor: string) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
    // O erro do campo some assim que ele é editado; o usuário vê o que ainda falta.
    setErros((atuais) => (atuais[campo] ? { ...atuais, [campo]: undefined } : atuais));
  }

  function limiteDe(campo: Campos) {
    return campo === 'observacoes' ? limiteObservacoes : LIMITES[campo];
  }

  /** RN-010 conferida antes de enviar, com o erro ao lado do campo que falta. */
  function validar(): boolean {
    const encontrados: Partial<Record<Campos, string>> = {};

    for (const { campo, rotulo } of CAMPOS_CLINICOS) {
      const valor = form[campo].trim();

      if (!valor) {
        encontrados[campo] = `Preencha ${rotulo}.`;
      } else if (valor.length > limiteDe(campo)) {
        encontrados[campo] = `Use no máximo ${limiteDe(campo)} caracteres.`;
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

    if (escolheVeterinario && !veterinarioId) {
      setErro((atual) => atual || 'Selecione o veterinário responsável pela avaliação.');
      return false;
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
      motivoConsulta: form.motivoConsulta.trim(),
      observacoes: form.observacoes.trim(),
      diagnosticoPossivel: form.diagnosticoPossivel.trim(),
    };

    try {
      if (avaliacao) {
        // RN-004: a API arquiva a versão anterior antes de aplicar a correção.
        await api.put(`/api/avaliacoes/${avaliacao.id}`, campos);
      } else {
        await api.post('/api/avaliacoes', {
          pacienteId,
          veterinarioId: veterinarioId || null,
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

  function contador(campo: Campos) {
    return `${form[campo].length}/${limiteDe(campo)}`;
  }

  return (
    <Modal
      aberto
      titulo={emEdicao ? 'Corrigir avaliação clínica' : 'Registrar avaliação clínica'}
      descricao={
        emEdicao
          ? 'A versão anterior fica guardada no histórico do prontuário (RN-004).'
          : 'Depois de registrada, a avaliação pode originar tratamentos, receitas e pedidos de exame.'
      }
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <form onSubmit={aoEnviar} className="space-y-4" noValidate>
        <Campo rotulo="Motivo da consulta" obrigatorio erro={erros.motivoConsulta} dica={contador('motivoConsulta')}>
          <textarea
            className="vc-campo"
            rows={2}
            value={form.motivoConsulta}
            onChange={(e) => atualizar('motivoConsulta', e.target.value)}
            placeholder="O que trouxe o animal à clínica."
            maxLength={LIMITES.motivoConsulta}
          />
        </Campo>

        <Campo rotulo="Observações" obrigatorio erro={erros.observacoes} dica={contador('observacoes')}>
          <textarea
            className="vc-campo"
            rows={4}
            value={form.observacoes}
            onChange={(e) => atualizar('observacoes', e.target.value)}
            placeholder="Histórico relatado pelo tutor, achados do exame físico, medicações em uso."
            maxLength={limiteObservacoes}
          />
        </Campo>

        <Campo
          rotulo="Possível diagnóstico"
          obrigatorio
          erro={erros.diagnosticoPossivel}
          dica={contador('diagnosticoPossivel')}
        >
          <textarea
            className="vc-campo"
            rows={2}
            value={form.diagnosticoPossivel}
            onChange={(e) => atualizar('diagnosticoPossivel', e.target.value)}
            maxLength={LIMITES.diagnosticoPossivel}
          />
        </Campo>

        {escolheVeterinario && (
          <SeletorVeterinario
            valor={veterinarioId}
            aoMudar={setVeterinarioId}
            obrigatorio
            dica={
              veterinarioResponsavelId
                ? `Sugerido: ${nomeVeterinarioResponsavel}, responsável pelo paciente. Qualquer veterinário da clínica pode assinar.`
                : 'O paciente ainda não tem veterinário responsável: quem assinar a avaliação passa a acompanhá-lo.'
            }
          />
        )}

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
    </Modal>
  );
}
