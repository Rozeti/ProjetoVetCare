import { useEffect, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import type { PaginaDe, Pet, Tratamento, Veterinario } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { paraIsoLocal, paraValorInputData } from '../../utils/formato';

interface Props {
  aberto: boolean;
  /** Quando informado, a sessão já nasce presa a este veterinário (agenda individual). */
  veterinarioId?: string;
  /** Pré-seleciona o paciente quando o modal é aberto a partir do prontuário. */
  pacienteId?: string;
  aoFechar: () => void;
  aoSalvar: () => void;
}

/** HU-004, CA-1: reserva de horário para uma sessão de fisioterapia. */
export function ModalAgendarSessao({ aberto, veterinarioId, pacienteId, aoFechar, aoSalvar }: Props) {
  // Enquanto fechado o modal não existe: não consome a API nem guarda seleção antiga.
  if (!aberto) {
    return null;
  }

  return (
    <Formulario
      veterinarioId={veterinarioId}
      pacienteId={pacienteId}
      aoFechar={aoFechar}
      aoSalvar={aoSalvar}
    />
  );
}

function Formulario({ veterinarioId, pacienteId, aoFechar, aoSalvar }: Omit<Props, 'aberto'>) {
  const [pets, setPets] = useState<Pet[]>([]);
  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  const [tratamentos, setTratamentos] = useState<Tratamento[]>([]);

  const [petSelecionado, setPetSelecionado] = useState(pacienteId ?? '');
  const [tratamentoId, setTratamentoId] = useState('');
  const [vetSelecionado, setVetSelecionado] = useState(veterinarioId ?? '');
  const [data, setData] = useState(paraValorInputData(new Date()));
  const [hora, setHora] = useState('09:00');
  const [observacoes, setObservacoes] = useState('');

  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    let ativo = true;

    async function carregarListas() {
      try {
        const requisicoes: Promise<unknown>[] = [
          api.get<PaginaDe<Pet>>('/api/pets', { params: { ativo: true, tamanho: 100 } }),
        ];

        if (!veterinarioId) {
          requisicoes.push(api.get<Veterinario[]>('/api/veterinarios'));
        }

        const [respostaPets, respostaVets] = await Promise.all(requisicoes);

        if (!ativo) return;

        setPets((respostaPets as { data: PaginaDe<Pet> }).data.itens);

        if (respostaVets) {
          setVeterinarios((respostaVets as { data: Veterinario[] }).data);
        }
      } catch (falha) {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os dados do agendamento.'));
      }
    }

    carregarListas();

    return () => {
      ativo = false;
    };
  }, [veterinarioId]);

  // Os tratamentos dependem do paciente escolhido, então são relidos a cada troca.
  useEffect(() => {
    let ativo = true;

    async function carregarTratamentos(paciente: string) {
      if (!paciente) {
        setTratamentos([]);
        setTratamentoId('');
        return;
      }

      try {
        const { data: lista } = await api.get<Tratamento[]>(`/api/tratamentos/paciente/${paciente}`);
        const ativos = lista.filter((t) => t.status === 'Em Andamento');

        if (!ativo) return;

        setTratamentos(ativos);
        // Com um único tratamento ativo, a escolha é óbvia e já vem preenchida.
        setTratamentoId(ativos.length === 1 ? ativos[0].id : '');
      } catch (falha) {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os tratamentos do paciente.'));
      }
    }

    carregarTratamentos(petSelecionado);

    return () => {
      ativo = false;
    };
  }, [petSelecionado]);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!tratamentoId) {
      setErro('Selecione o paciente e o tratamento em andamento para agendar a sessão.');
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/sessoes', {
        tratamentoId,
        veterinarioId: vetSelecionado || null,
        dataHora: paraIsoLocal(data, hora),
        observacoes,
      });

      setObservacoes('');
      aoSalvar();
    } catch (falha) {
      // Conflito de horário (RN-002) e agendamento retroativo (CA-5) chegam por aqui.
      setErro(mensagemDeErro(falha, 'Não foi possível agendar a sessão.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal
      aberto
      titulo="Agendar sessão"
      descricao="A sessão é criada com o status Aguardando confirmação."
      aoFechar={aoFechar}
    >
      <form onSubmit={aoEnviar} className="space-y-4">
        <Campo rotulo="Paciente" obrigatorio>
          <select
            className="vc-campo"
            value={petSelecionado}
            onChange={(e) => setPetSelecionado(e.target.value)}
            disabled={!!pacienteId}
          >
            <option value="">Selecione o paciente</option>
            {pets.map((pet) => (
              <option key={pet.id} value={pet.id}>
                {pet.nome} — {pet.nomeTutor}
              </option>
            ))}
          </select>
        </Campo>

        <Campo
          rotulo="Tratamento"
          obrigatorio
          dica={
            petSelecionado && tratamentos.length === 0
              ? 'Este paciente não possui tratamento em andamento. Abra um tratamento no prontuário antes de agendar.'
              : undefined
          }
        >
          <select
            className="vc-campo"
            value={tratamentoId}
            onChange={(e) => setTratamentoId(e.target.value)}
            disabled={tratamentos.length === 0}
          >
            <option value="">Selecione o tratamento</option>
            {tratamentos.map((tratamento) => (
              <option key={tratamento.id} value={tratamento.id}>
                {tratamento.objetivoTerapeutico.slice(0, 60)}
              </option>
            ))}
          </select>
        </Campo>

        {!veterinarioId && (
          <Campo rotulo="Veterinário" obrigatorio>
            <select className="vc-campo" value={vetSelecionado} onChange={(e) => setVetSelecionado(e.target.value)}>
              <option value="">Selecione o profissional</option>
              {veterinarios.map((vet) => (
                <option key={vet.id} value={vet.id}>
                  {vet.nome} — {vet.especialidade}
                </option>
              ))}
            </select>
          </Campo>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo rotulo="Data" obrigatorio>
            <input type="date" className="vc-campo" value={data} onChange={(e) => setData(e.target.value)} />
          </Campo>

          <Campo rotulo="Horário" obrigatorio>
            <input type="time" className="vc-campo" value={hora} onChange={(e) => setHora(e.target.value)} />
          </Campo>
        </div>

        <Campo rotulo="Observações" dica="Orientações que o tutor deve seguir antes da sessão.">
          <textarea
            className="vc-campo"
            rows={2}
            value={observacoes}
            onChange={(e) => setObservacoes(e.target.value)}
            placeholder="Ex.: trazer o pet em jejum de duas horas."
          />
        </Campo>

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            Agendar sessão
          </button>
        </div>
      </form>
    </Modal>
  );
}
