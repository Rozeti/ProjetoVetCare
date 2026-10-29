import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { Avaliacao, StatusTratamento, Tratamento } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { SeletorVeterinario } from '../../components/SeletorVeterinario';
import { formatarData, paraValorInputData, tratamentoAtivo } from '../../utils/formato';

/** Mesmos limites de CriarTratamentoDTO. */
const LIMITES = {
  nome: 120,
  tipo: 60,
  descricao: 2000,
  duracaoPrevista: 60,
  frequencia: 60,
  observacoesGerais: 2000,
};

const SESSOES_MAXIMAS = 365;

/** Sugestões do campo "Tipo"; a API aceita qualquer texto. */
const TIPOS_SUGERIDOS = [
  'Fisioterapia',
  'Hidroterapia',
  'Acupuntura',
  'Reabilitação pós-cirúrgica',
  'Medicamentoso',
  'Cirúrgico',
  'Nutricional',
];

interface Props {
  aberto: boolean;
  /** Avaliação de origem: obrigatória para criar um tratamento. */
  avaliacao?: Pick<Avaliacao, 'id' | 'dataRegistro' | 'motivoConsulta'> | null;
  /** Quando informado, o modal edita este tratamento. */
  tratamento?: Tratamento | null;
  /** Veterinário que acompanha o paciente, sugerido para conduzir. Nulo quando ainda não há responsável. */
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel?: string;
  aoFechar: () => void;
  aoSalvar: (mensagem: string) => void;
}

/**
 * Tratamento: procedimento ou plano terapêutico encaminhado a partir de uma avaliação. As
 * sessões realizadas dentro dele geram as evoluções.
 */
export function ModalTratamento({ aberto, ...props }: Props) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos
  // novos, o que dispensa um efeito para limpá-los.
  if (!aberto) {
    return null;
  }

  return <Formulario {...props} />;
}

function Formulario({
  avaliacao,
  tratamento,
  veterinarioResponsavelId,
  nomeVeterinarioResponsavel,
  aoFechar,
  aoSalvar,
}: Omit<Props, 'aberto'>) {
  const { ehVeterinario } = useAuth();

  const emEdicao = !!tratamento;
  // O veterinário conduz o tratamento que cadastra; administração e apoio escolhem quem conduz,
  // com o responsável pelo paciente já sugerido.
  const escolheVeterinario = !emEdicao && !ehVeterinario;
  // Concluído e inativado mudam de situação pelos botões do tratamento, não pelo formulário.
  const editaStatus = !emEdicao || tratamentoAtivo(tratamento.status);

  const [form, setForm] = useState(() => ({
    nome: tratamento?.nome ?? '',
    tipo: tratamento?.tipo ?? 'Fisioterapia',
    descricao: tratamento?.descricao ?? '',
    duracaoPrevista: tratamento?.duracaoPrevista ?? '',
    frequencia: tratamento?.frequencia ?? '',
    sessoesPrevistas: tratamento ? String(tratamento.sessoesPrevistas) : '',
    status: (tratamento?.status ?? 'Planejado') as StatusTratamento,
    dataInicio: paraValorInputData(tratamento?.dataInicio ?? new Date()),
    observacoesGerais: tratamento?.observacoesGerais ?? '',
  }));
  const [veterinarioId, setVeterinarioId] = useState(veterinarioResponsavelId ?? '');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const hoje = paraValorInputData(new Date());

  function atualizar(campo: keyof typeof form, valor: string) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
  }

  /** Mesmas regras de CriarTratamentoDTO e do caso de uso, conferidas antes do envio. */
  function validar(): string | null {
    const nome = form.nome.trim();
    const descricao = form.descricao.trim();
    const sessoes = Number(form.sessoesPrevistas);

    if (nome.length < 3) return 'Informe o nome do tratamento (mínimo de 3 caracteres).';
    if (nome.length > LIMITES.nome) return `O nome do tratamento deve ter até ${LIMITES.nome} caracteres.`;
    if (!form.tipo.trim()) return 'Informe o tipo do tratamento.';
    if (form.tipo.trim().length > LIMITES.tipo) return `O tipo deve ter até ${LIMITES.tipo} caracteres.`;
    if (descricao.length < 3) return 'Descreva o tratamento (mínimo de 3 caracteres).';
    if (!form.duracaoPrevista.trim()) return 'Informe a duração prevista do tratamento.';
    if (!form.frequencia.trim()) return 'Informe a frequência das sessões.';

    if (!Number.isInteger(sessoes) || sessoes < 1 || sessoes > SESSOES_MAXIMAS) {
      return `Informe entre 1 e ${SESSOES_MAXIMAS} sessões previstas.`;
    }

    if (!form.dataInicio) return 'Informe a data de início do tratamento.';

    if (form.status === 'Em Andamento' && form.dataInicio > hoje) {
      return 'Um tratamento em andamento não pode ter início no futuro. Cadastre-o como planejado.';
    }

    if (escolheVeterinario && !veterinarioId) return 'Selecione o veterinário responsável pelo tratamento.';

    return null;
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    const problema = validar();

    if (problema) {
      setErro(problema);
      return;
    }

    setSalvando(true);

    const campos = {
      nome: form.nome.trim(),
      tipo: form.tipo.trim(),
      descricao: form.descricao.trim(),
      duracaoPrevista: form.duracaoPrevista.trim(),
      frequencia: form.frequencia.trim(),
      sessoesPrevistas: Number(form.sessoesPrevistas),
      observacoesGerais: form.observacoesGerais.trim(),
    };

    try {
      if (tratamento) {
        const dataMudou = form.dataInicio !== paraValorInputData(tratamento.dataInicio);

        await api.put(`/api/tratamentos/${tratamento.id}`, {
          ...campos,
          dataInicio: dataMudou ? form.dataInicio : null,
          status: editaStatus && form.status !== tratamento.status ? form.status : null,
        });

        aoSalvar('Tratamento atualizado com sucesso.');
      } else {
        await api.post('/api/tratamentos', {
          ...campos,
          avaliacaoId: avaliacao?.id,
          veterinarioId: veterinarioId || null,
          dataInicio: form.dataInicio,
          status: form.status,
        });

        aoSalvar(
          form.status === 'Planejado'
            ? 'Tratamento planejado. Ele passa a "em andamento" na primeira sessão registrada.'
            : 'Tratamento iniciado com sucesso.',
        );
      }
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível salvar o tratamento.'));
    } finally {
      setSalvando(false);
    }
  }

  const origem = tratamento
    ? { data: tratamento.dataAvaliacao, motivo: tratamento.motivoAvaliacao }
    : { data: avaliacao?.dataRegistro, motivo: avaliacao?.motivoConsulta };

  return (
    <Modal
      aberto
      titulo={emEdicao ? 'Editar tratamento' : 'Novo tratamento'}
      descricao={
        emEdicao
          ? 'Atualize o plano terapêutico. As sessões já registradas não mudam.'
          : 'O tratamento é encaminhado a partir da avaliação abaixo. Cada sessão realizada é registrada nele.'
      }
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <form onSubmit={aoEnviar} className="space-y-4" noValidate>
        <div className="rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <span className="font-semibold text-slate-700">Avaliação de origem</span>
          {origem.data && ` · ${formatarData(origem.data)}`}
          {origem.motivo && <p className="mt-0.5 line-clamp-2">{origem.motivo}</p>}
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <div className="sm:col-span-2">
            <Campo rotulo="Nome do tratamento" obrigatorio>
              <input
                className="vc-campo"
                value={form.nome}
                onChange={(e) => atualizar('nome', e.target.value)}
                placeholder="Ex.: Reabilitação do membro posterior direito"
                maxLength={LIMITES.nome}
              />
            </Campo>
          </div>

          <Campo rotulo="Tipo" obrigatorio>
            <input
              className="vc-campo"
              list="tipos-de-tratamento"
              value={form.tipo}
              onChange={(e) => atualizar('tipo', e.target.value)}
              maxLength={LIMITES.tipo}
            />
          </Campo>
          <datalist id="tipos-de-tratamento">
            {TIPOS_SUGERIDOS.map((tipo) => (
              <option key={tipo} value={tipo} />
            ))}
          </datalist>
        </div>

        <Campo rotulo="Descrição" obrigatorio dica={`${form.descricao.length}/${LIMITES.descricao} caracteres`}>
          <textarea
            className="vc-campo"
            rows={3}
            value={form.descricao}
            onChange={(e) => atualizar('descricao', e.target.value)}
            placeholder="Técnicas previstas, objetivos e metas do plano terapêutico."
            maxLength={LIMITES.descricao}
          />
        </Campo>

        <div className="grid gap-4 sm:grid-cols-3">
          <Campo rotulo="Duração prevista" obrigatorio>
            <input
              className="vc-campo"
              value={form.duracaoPrevista}
              onChange={(e) => atualizar('duracaoPrevista', e.target.value)}
              placeholder="Ex.: 8 semanas"
              maxLength={LIMITES.duracaoPrevista}
            />
          </Campo>

          <Campo rotulo="Frequência" obrigatorio>
            <input
              className="vc-campo"
              value={form.frequencia}
              onChange={(e) => atualizar('frequencia', e.target.value)}
              placeholder="Ex.: 2 sessões por semana"
              maxLength={LIMITES.frequencia}
            />
          </Campo>

          <Campo rotulo="Sessões previstas" obrigatorio>
            <input
              type="number"
              min={1}
              max={SESSOES_MAXIMAS}
              className="vc-campo"
              value={form.sessoesPrevistas}
              onChange={(e) => atualizar('sessoesPrevistas', e.target.value)}
            />
          </Campo>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {editaStatus && (
            <Campo
              rotulo="Status"
              obrigatorio
              dica="Planejado passa a em andamento na primeira sessão registrada."
            >
              <select
                className="vc-campo"
                value={form.status}
                onChange={(e) => atualizar('status', e.target.value)}
              >
                <option value="Planejado">Planejado</option>
                <option value="Em Andamento">Em andamento</option>
              </select>
            </Campo>
          )}

          <Campo
            rotulo="Data de início"
            obrigatorio
            dica={form.status === 'Planejado' ? 'Pode ser uma data futura.' : 'Hoje ou uma data passada.'}
          >
            <input
              type="date"
              className="vc-campo"
              max={form.status === 'Planejado' ? undefined : hoje}
              value={form.dataInicio}
              onChange={(e) => atualizar('dataInicio', e.target.value)}
            />
          </Campo>
        </div>

        {escolheVeterinario ? (
          <SeletorVeterinario
            valor={veterinarioId}
            aoMudar={setVeterinarioId}
            obrigatorio
            rotulo="Veterinário que conduz o tratamento"
            dica={
              veterinarioResponsavelId
                ? `Sugerido: ${nomeVeterinarioResponsavel}, responsável pelo paciente. Qualquer veterinário da clínica pode conduzir.`
                : 'O paciente ainda não tem veterinário responsável: quem conduzir o tratamento passa a acompanhá-lo.'
            }
          />
        ) : (
          <p className="text-sm text-slate-600">
            Veterinário: <strong>{tratamento ? tratamento.nomeVeterinario : 'você'}</strong>
          </p>
        )}

        <Campo
          rotulo="Observações gerais"
          dica={`${form.observacoesGerais.length}/${LIMITES.observacoesGerais} caracteres`}
        >
          <textarea
            className="vc-campo"
            rows={2}
            value={form.observacoesGerais}
            onChange={(e) => atualizar('observacoesGerais', e.target.value)}
            maxLength={LIMITES.observacoesGerais}
          />
        </Campo>

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            {emEdicao ? 'Salvar alterações' : 'Cadastrar tratamento'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
