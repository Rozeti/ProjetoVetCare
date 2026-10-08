import { useEffect, useState, type FormEvent } from 'react';
import { ArrowRightLeft, Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import type { ResultadoTransferencia, Veterinario } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';

/** O mínimo que a tela precisa saber do paciente para transferi-lo. */
export interface PacienteParaTransferir {
  id: string;
  nome: string;
  veterinarioResponsavelId?: string | null;
  nomeVeterinarioResponsavel: string;
}

interface Props {
  paciente: PacienteParaTransferir | null;
  aoFechar: () => void;
  /** Recebe a frase pronta para a tela exibir como confirmação. */
  aoTransferir: (mensagem: string) => void;
}

/**
 * Troca o veterinário responsável. O paciente sai da lista do profissional atual e
 * leva junto os tratamentos em andamento e as sessões futuras; o histórico já
 * registrado continua assinado por quem o produziu.
 */
export function ModalTransferirPaciente({ paciente, aoFechar, aoTransferir }: Props) {
  // Fechado o modal não existe: cada abertura começa com os campos limpos.
  if (!paciente) {
    return null;
  }

  return <Formulario paciente={paciente} aoFechar={aoFechar} aoTransferir={aoTransferir} />;
}

function Formulario({ paciente, aoFechar, aoTransferir }: Props & { paciente: PacienteParaTransferir }) {
  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  const [veterinarioId, setVeterinarioId] = useState('');
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const temResponsavel = !!paciente.veterinarioResponsavelId;
  const { confirmar } = useConfirmacao();

  useEffect(() => {
    let ativo = true;

    api
      .get<Veterinario[]>('/api/veterinarios')
      .then(({ data }) => {
        if (!ativo) return;
        // Só profissionais ativos recebem pacientes, e o atual não aparece como destino.
        setVeterinarios(data.filter((v) => v.ativo && v.id !== paciente.veterinarioResponsavelId));
      })
      .catch((falha) => {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os veterinários.'));
      });

    return () => {
      ativo = false;
    };
  }, [paciente.veterinarioResponsavelId]);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!veterinarioId) {
      setErro('Selecione o veterinário que passa a acompanhar o paciente.');
      return;
    }

    const destino = veterinarios.find((v) => v.id === veterinarioId)?.nome ?? 'o novo veterinário';

    // A transferência move tratamentos e sessões de agenda e não tem desfazer: confirma antes.
    if (
      !(await confirmar({
        titulo: temResponsavel ? 'Confirmar transferência' : 'Confirmar responsável',
        mensagem: temResponsavel ? (
          <>
            Transferir <strong>{paciente.nome}</strong> de {paciente.nomeVeterinarioResponsavel} para{' '}
            <strong>{destino}</strong>? Os tratamentos em andamento e as sessões futuras vão junto, e o paciente
            deixa de aparecer para o profissional atual.
          </>
        ) : (
          <>
            Definir <strong>{destino}</strong> como responsável por <strong>{paciente.nome}</strong>?
          </>
        ),
        rotuloConfirmar: temResponsavel ? 'Transferir' : 'Definir responsável',
        perigo: temResponsavel,
      }))
    ) {
      return;
    }

    setSalvando(true);

    try {
      const { data } = await api.patch<ResultadoTransferencia>(`/api/pets/${paciente.id}/veterinario-responsavel`, {
        veterinarioId,
        motivo: motivo.trim(),
      });

      aoTransferir(descrever(data));
    } catch (falha) {
      // A agenda do novo veterinário ocupada nos horários das sessões futuras chega por aqui.
      setErro(mensagemDeErro(falha, 'Não foi possível transferir o paciente.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal
      aberto
      titulo={temResponsavel ? 'Transferir paciente' : 'Definir veterinário responsável'}
      descricao={
        temResponsavel
          ? `${paciente.nome} é acompanhado por ${paciente.nomeVeterinarioResponsavel}.`
          : `${paciente.nome} ainda não tem um veterinário designado.`
      }
      aoFechar={aoFechar}
    >
      <form onSubmit={aoEnviar} className="space-y-4">
        <Campo rotulo="Novo veterinário responsável" obrigatorio>
          <select className="vc-campo" value={veterinarioId} onChange={(e) => setVeterinarioId(e.target.value)}>
            <option value="">Selecione o profissional</option>
            {veterinarios.map((vet) => (
              <option key={vet.id} value={vet.id}>
                {vet.nome}
                {vet.especialidade && ` — ${vet.especialidade}`}
              </option>
            ))}
          </select>
        </Campo>

        <Campo rotulo="Motivo" dica="Opcional. Fica registrado na auditoria da clínica.">
          <input
            className="vc-campo"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="Ex.: férias do profissional, mudança de especialidade"
            maxLength={300}
          />
        </Campo>

        <Alerta tipo="info">
          {temResponsavel
            ? 'Os tratamentos em andamento e as sessões futuras passam para a agenda do novo veterinário, e o paciente deixa de aparecer para o profissional atual. O histórico já registrado não muda.'
            : 'A partir de agora só o veterinário escolhido (além da administração) verá este paciente e poderá abrir tratamentos para ele.'}
        </Alerta>

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando ? <Loader2 className="animate-spin" size={16} /> : <ArrowRightLeft size={16} />}
            {temResponsavel ? 'Transferir paciente' : 'Definir responsável'}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function descrever(resultado: ResultadoTransferencia): string {
  const partes: string[] = [];

  if (resultado.tratamentosTransferidos > 0) {
    partes.push(
      resultado.tratamentosTransferidos === 1
        ? '1 tratamento em andamento'
        : `${resultado.tratamentosTransferidos} tratamentos em andamento`,
    );
  }

  if (resultado.sessoesTransferidas > 0) {
    partes.push(
      resultado.sessoesTransferidas === 1 ? '1 sessão futura' : `${resultado.sessoesTransferidas} sessões futuras`,
    );
  }

  const complemento = partes.length > 0 ? ` ${partes.join(' e ')} passaram para a agenda de ${resultado.nomeNovoVeterinario}.` : '';

  return `${resultado.paciente.nome} agora é acompanhado por ${resultado.nomeNovoVeterinario}.${complemento}`;
}
