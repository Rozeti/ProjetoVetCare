import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import type { PetSelecao, Tratamento } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { tratamentoAtivo } from '../../utils/formato';

interface Props {
  aberto: boolean;
  /** Pacientes ativos da clínica (a mesma lista dos filtros da tela). */
  pacientes: PetSelecao[];
  /** Animal e tratamento já escolhidos nos filtros, quando houver. */
  pacienteInicial?: string;
  tratamentoInicial?: string;
  aoFechar: () => void;
  aoEscolher: (tratamento: Tratamento, paciente: PetSelecao) => void;
}

/**
 * Primeiro passo do registro de uma sessão fora do prontuário: escolher o animal e o
 * tratamento em andamento. A evolução em si é preenchida no passo seguinte.
 */
export function ModalEscolherTratamento({ aberto, ...props }: Props) {
  // Fechado, o modal não existe: cada abertura começa da seleção dos filtros.
  if (!aberto) return null;

  return <Formulario {...props} />;
}

function Formulario({ pacientes, pacienteInicial = '', tratamentoInicial = '', aoFechar, aoEscolher }: Omit<Props, 'aberto'>) {
  const [pacienteId, setPacienteId] = useState(pacienteInicial);
  const [tratamentoId, setTratamentoId] = useState(tratamentoInicial);
  const [tratamentos, setTratamentos] = useState<Tratamento[] | null>(null);
  const [erro, setErro] = useState('');

  // Só tratamentos planejados ou em andamento recebem sessões novas.
  useEffect(() => {
    if (!pacienteId) return;

    let ativo = true;

    api
      .get<Tratamento[]>(`/api/tratamentos/paciente/${pacienteId}`)
      .then(({ data }) => {
        if (!ativo) return;

        const emCurso = data.filter((t) => tratamentoAtivo(t.status));
        setTratamentos(emCurso);
        setTratamentoId((atual) => (emCurso.some((t) => t.id === atual) ? atual : emCurso.length === 1 ? emCurso[0].id : ''));
      })
      .catch((falha) => {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os tratamentos do animal.'));
      });

    return () => {
      ativo = false;
    };
  }, [pacienteId]);

  function escolherPaciente(id: string) {
    setPacienteId(id);
    setTratamentos(null);
    setTratamentoId('');
    setErro('');
  }

  function continuar(evento: FormEvent) {
    evento.preventDefault();

    const paciente = pacientes.find((p) => p.id === pacienteId);
    const tratamento = tratamentos?.find((t) => t.id === tratamentoId);

    if (!paciente || !tratamento) {
      setErro('Escolha o animal e o tratamento da sessão.');
      return;
    }

    aoEscolher(tratamento, paciente);
  }

  const carregandoTratamentos = !!pacienteId && tratamentos === null && !erro;
  const semTratamento = tratamentos !== null && tratamentos.length === 0;
  const escolhido = tratamentos?.find((t) => t.id === tratamentoId);

  return (
    <Modal
      aberto
      titulo="Registrar sessão"
      descricao="Escolha o animal e o tratamento; em seguida, anote o que foi feito na sessão."
      aoFechar={aoFechar}
    >
      <form onSubmit={continuar} className="space-y-4" noValidate>
        <Campo rotulo="Animal" obrigatorio>
          <select className="vc-campo" value={pacienteId} onChange={(e) => escolherPaciente(e.target.value)}>
            <option value="">Selecione o animal</option>
            {pacientes.map((paciente) => (
              <option key={paciente.id} value={paciente.id}>
                {paciente.nome} — {paciente.nomeTutor}
              </option>
            ))}
          </select>
        </Campo>

        <Campo
          rotulo="Tratamento"
          obrigatorio
          dica={
            escolhido
              ? `${escolhido.evolucoesRegistradas} de ${escolhido.sessoesPrevistas} sessões já registradas; esta será a nº ${escolhido.evolucoesRegistradas + 1}.`
              : semTratamento
                ? undefined
                : 'Somente tratamentos planejados ou em andamento.'
          }
        >
          <select
            className="vc-campo"
            value={tratamentoId}
            onChange={(e) => setTratamentoId(e.target.value)}
            disabled={!pacienteId || carregandoTratamentos || semTratamento}
          >
            <option value="">
              {!pacienteId ? 'Escolha o animal primeiro' : carregandoTratamentos ? 'Carregando...' : 'Selecione o tratamento'}
            </option>
            {tratamentos?.map((tratamento) => (
              <option key={tratamento.id} value={tratamento.id}>
                {tratamento.nome}
              </option>
            ))}
          </select>
        </Campo>

        {semTratamento && (
          <Alerta tipo="info">
            Este animal não tem tratamento em andamento. O tratamento é criado a partir de uma avaliação,{' '}
            <Link to={`/prontuario/${pacienteId}?aba=avaliacoes`} className="font-semibold underline">
              no prontuário
            </Link>
            .
          </Alerta>
        )}

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-1">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={!tratamentoId}>
            {carregandoTratamentos && <Loader2 className="animate-spin" size={16} />}
            Continuar
          </button>
        </div>
      </form>
    </Modal>
  );
}
