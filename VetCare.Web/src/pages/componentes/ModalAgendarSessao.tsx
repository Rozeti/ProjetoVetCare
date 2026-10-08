import { useEffect, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import type { PetSelecao, Tratamento, Veterinario } from '../../types';
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

/** Mesmo limite de AgendarSessaoDTO. */
const TAMANHO_OBSERVACOES = 1000;

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
  const [pets, setPets] = useState<PetSelecao[]>([]);
  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  const [tratamentos, setTratamentos] = useState<Tratamento[]>([]);
  const [carregandoListas, setCarregandoListas] = useState(true);

  const [petSelecionado, setPetSelecionado] = useState(pacienteId ?? '');
  const [tratamentoId, setTratamentoId] = useState('');
  const [vetSelecionado, setVetSelecionado] = useState(veterinarioId ?? '');
  const [buscaPaciente, setBuscaPaciente] = useState('');

  // A sessão entra na agenda de quem acompanha o paciente: escolhido o pet, o
  // veterinário responsável vem junto e a lista fica travada nele.
  const responsavelDoPet = pets.find((p) => p.id === petSelecionado)?.veterinarioResponsavelId ?? '';
  const [data, setData] = useState(paraValorInputData(new Date()));
  const [hora, setHora] = useState('09:00');
  const [observacoes, setObservacoes] = useState('');

  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    let ativo = true;

    async function carregarListas() {
      try {
        // A lista de seleção traz todos os pacientes ativos do recorte de quem agenda,
        // sem o teto de página da listagem principal.
        const respostaPets = await api.get<PetSelecao[]>('/api/pets/selecao');
        // Só profissionais ativos recebem sessões novas.
        const respostaVets = veterinarioId ? null : await api.get<Veterinario[]>('/api/veterinarios');

        if (!ativo) return;

        setPets(respostaPets.data);

        if (respostaVets) {
          setVeterinarios(respostaVets.data.filter((v) => v.ativo));
        }
      } catch (falha) {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os dados do agendamento.'));
      } finally {
        if (ativo) setCarregandoListas(false);
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

  const hoje = paraValorInputData(new Date());

  // Com muitos pacientes, a busca reduz a lista; o selecionado nunca sai dela.
  const termo = buscaPaciente.trim().toLowerCase();
  const petsVisiveis = termo
    ? pets.filter(
        (p) => p.id === petSelecionado || p.nome.toLowerCase().includes(termo) || p.nomeTutor.toLowerCase().includes(termo),
      )
    : pets;

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!petSelecionado) {
      setErro('Selecione o paciente da sessão.');
      return;
    }

    if (!tratamentoId) {
      setErro('Selecione o tratamento em andamento para agendar a sessão.');
      return;
    }

    const veterinarioDaSessao = responsavelDoPet || vetSelecionado;

    if (!veterinarioId && !veterinarioDaSessao) {
      setErro('Selecione o veterinário responsável pela sessão.');
      return;
    }

    if (!data || !hora) {
      setErro('Informe a data e o horário da sessão.');
      return;
    }

    // HU-004, CA-5: agendamento retroativo é bloqueado já aqui, antes da API.
    if (new Date(`${data}T${hora}:00`).getTime() <= Date.now()) {
      setErro('A sessão precisa ser agendada para uma data e um horário futuros.');
      return;
    }

    if (observacoes.length > TAMANHO_OBSERVACOES) {
      setErro(`As observações devem ter até ${TAMANHO_OBSERVACOES} caracteres.`);
      return;
    }

    setSalvando(true);

    try {
      await api.post('/api/sessoes', {
        tratamentoId,
        veterinarioId: veterinarioDaSessao || null,
        dataHora: paraIsoLocal(data, hora),
        observacoes: observacoes.trim(),
      });

      setObservacoes('');
      aoSalvar();
    } catch (falha) {
      // Conflito de horário (RN-002), bloqueio de agenda e horário fora do expediente chegam por aqui.
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
      <form onSubmit={aoEnviar} className="space-y-4" noValidate>
        {!pacienteId && pets.length > 8 && (
          <Campo rotulo="Buscar paciente" dica="Filtra a lista abaixo pelo nome do pet ou do tutor.">
            <input
              className="vc-campo"
              value={buscaPaciente}
              onChange={(e) => setBuscaPaciente(e.target.value)}
              placeholder="Ex.: Thor, Maria"
            />
          </Campo>
        )}

        <Campo
          rotulo="Paciente"
          obrigatorio
          dica={
            !carregandoListas && pets.length === 0
              ? 'Nenhum paciente ativo disponível para agendamento.'
              : undefined
          }
        >
          <select
            className="vc-campo"
            value={petSelecionado}
            onChange={(e) => setPetSelecionado(e.target.value)}
            disabled={!!pacienteId || carregandoListas}
          >
            <option value="">{carregandoListas ? 'Carregando pacientes...' : 'Selecione o paciente'}</option>
            {petsVisiveis.map((pet) => (
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
          <Campo
            rotulo="Veterinário"
            obrigatorio
            dica={responsavelDoPet ? 'Veterinário responsável pelo paciente. Para trocar, transfira o paciente.' : undefined}
          >
            <select
              className="vc-campo"
              value={responsavelDoPet || vetSelecionado}
              onChange={(e) => setVetSelecionado(e.target.value)}
              disabled={!!responsavelDoPet}
            >
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
            <input type="date" className="vc-campo" min={hoje} value={data} onChange={(e) => setData(e.target.value)} />
          </Campo>

          <Campo rotulo="Horário" obrigatorio dica="Dentro do expediente da clínica.">
            <input type="time" className="vc-campo" value={hora} onChange={(e) => setHora(e.target.value)} />
          </Campo>
        </div>

        <Campo
          rotulo="Observações"
          dica={`Orientações que o tutor deve seguir antes da sessão. ${observacoes.length}/${TAMANHO_OBSERVACOES}`}
        >
          <textarea
            className="vc-campo"
            rows={2}
            value={observacoes}
            onChange={(e) => setObservacoes(e.target.value)}
            placeholder="Ex.: trazer o pet em jejum de duas horas."
            maxLength={TAMANHO_OBSERVACOES}
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
