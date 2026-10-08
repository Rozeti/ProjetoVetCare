import { useEffect, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { Veterinario } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { paraValorInputData } from '../../utils/formato';

/** Mesmos limites de CriarTratamentoDTO. */
const LIMITE_OBJETIVO = 1000;
const LIMITE_OBSERVACOES = 2000;

interface Props {
  aberto: boolean;
  pacienteId: string;
  /** Veterinário que acompanha o paciente: o tratamento é dele. Nulo quando ainda não há responsável. */
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel?: string;
  aoFechar: () => void;
  aoSalvar: () => void;
}

/**
 * Abertura do processo terapêutico que agrupa avaliação, sessões e atendimentos
 * de um paciente, conforme a definição de Tratamento no DAS.
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
  pacienteId,
  veterinarioResponsavelId,
  nomeVeterinarioResponsavel,
  aoFechar,
  aoSalvar,
}: Omit<Props, 'aberto'>) {
  const { usuario, ehVeterinario } = useAuth();

  // O paciente já tem quem o acompanha: o tratamento nasce no nome dele. Sem responsável
  // (cadastro vindo do aplicativo), o primeiro tratamento define quem passa a ser.
  const responsavelDefinido = !!veterinarioResponsavelId;

  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  const [veterinarioId, setVeterinarioId] = useState(
    veterinarioResponsavelId ?? (ehVeterinario ? usuario?.veterinarioId ?? '' : ''),
  );
  const [dataInicio, setDataInicio] = useState(paraValorInputData(new Date()));
  const [objetivo, setObjetivo] = useState('');
  const [observacoes, setObservacoes] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    // Com o responsável definido, ou sendo o próprio veterinário, não há o que escolher.
    if (responsavelDefinido || (ehVeterinario && usuario?.veterinarioId)) {
      return;
    }

    let ativo = true;

    api
      .get<Veterinario[]>('/api/veterinarios')
      .then(({ data }) => {
        // Só profissionais ativos assumem tratamentos novos.
        if (ativo) setVeterinarios(data.filter((v) => v.ativo));
      })
      .catch((falha) => {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os veterinários.'));
      });

    return () => {
      ativo = false;
    };
  }, [responsavelDefinido, ehVeterinario, usuario?.veterinarioId]);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    // Mesmas regras de CriarTratamentoDTO, conferidas antes do envio.
    const objetivoLimpo = objetivo.trim();

    if (objetivoLimpo.length < 3) {
      setErro('Descreva o objetivo terapêutico do tratamento (mínimo de 3 caracteres).');
      return;
    }

    if (objetivoLimpo.length > LIMITE_OBJETIVO) {
      setErro(`O objetivo terapêutico deve ter até ${LIMITE_OBJETIVO} caracteres.`);
      return;
    }

    if (observacoes.length > LIMITE_OBSERVACOES) {
      setErro(`As observações devem ter até ${LIMITE_OBSERVACOES} caracteres.`);
      return;
    }

    if (!dataInicio) {
      setErro('Informe a data de início do tratamento.');
      return;
    }

    if (dataInicio > paraValorInputData(new Date())) {
      setErro('A data de início do tratamento não pode ser futura.');
      return;
    }

    if (!veterinarioId) {
      setErro('Selecione o veterinário responsável.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/tratamentos', {
        pacienteId,
        veterinarioId,
        dataInicio,
        objetivoTerapeutico: objetivoLimpo,
        observacoesGerais: observacoes.trim(),
      });

      aoSalvar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível abrir o tratamento.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal
      aberto
      titulo="Novo tratamento"
      descricao="O tratamento agrupa a avaliação, as sessões e os atendimentos do paciente."
      aoFechar={aoFechar}
    >
      <form onSubmit={aoEnviar} className="space-y-4" noValidate>
        <Campo rotulo="Objetivo terapêutico" obrigatorio dica={`${objetivo.length}/${LIMITE_OBJETIVO} caracteres`}>
          <textarea
            className="vc-campo"
            rows={3}
            value={objetivo}
            onChange={(e) => setObjetivo(e.target.value)}
            placeholder="Ex.: recuperar a amplitude de movimento do membro posterior direito após cirurgia."
            maxLength={LIMITE_OBJETIVO}
          />
        </Campo>

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo
            rotulo="Veterinário responsável"
            obrigatorio
            dica={responsavelDefinido ? 'Definido pelo cadastro do paciente. Para trocar, transfira o paciente.' : undefined}
          >
            <select
              className="vc-campo"
              value={veterinarioId}
              onChange={(e) => setVeterinarioId(e.target.value)}
              disabled={responsavelDefinido || (ehVeterinario && !!usuario?.veterinarioId)}
            >
              <option value="">Selecione</option>
              {responsavelDefinido && (
                <option value={veterinarioResponsavelId ?? ''}>{nomeVeterinarioResponsavel}</option>
              )}
              {!responsavelDefinido && ehVeterinario && usuario?.veterinarioId && (
                <option value={usuario.veterinarioId}>{usuario.nome}</option>
              )}
              {veterinarios.map((vet) => (
                <option key={vet.id} value={vet.id}>
                  {vet.nome}
                </option>
              ))}
            </select>
          </Campo>

          <Campo rotulo="Data de início" obrigatorio dica="Hoje ou uma data passada.">
            <input
              type="date"
              className="vc-campo"
              max={paraValorInputData(new Date())}
              value={dataInicio}
              onChange={(e) => setDataInicio(e.target.value)}
            />
          </Campo>
        </div>

        <Campo rotulo="Observações gerais" dica={`${observacoes.length}/${LIMITE_OBSERVACOES} caracteres`}>
          <textarea
            className="vc-campo"
            rows={2}
            value={observacoes}
            onChange={(e) => setObservacoes(e.target.value)}
            maxLength={LIMITE_OBSERVACOES}
          />
        </Campo>

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            Abrir tratamento
          </button>
        </div>
      </form>
    </Modal>
  );
}
