import { useEffect, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { Veterinario } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { paraValorInputData } from '../../utils/formato';

interface Props {
  aberto: boolean;
  pacienteId: string;
  aoFechar: () => void;
  aoSalvar: () => void;
}

/**
 * Abertura do processo terapêutico que agrupa avaliação, sessões e atendimentos
 * de um paciente, conforme a definição de Tratamento no DAS.
 */
export function ModalTratamento({ aberto, pacienteId, aoFechar, aoSalvar }: Props) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos
  // novos, o que dispensa um efeito para limpá-los.
  if (!aberto) {
    return null;
  }

  return <Formulario pacienteId={pacienteId} aoFechar={aoFechar} aoSalvar={aoSalvar} />;
}

function Formulario({ pacienteId, aoFechar, aoSalvar }: Omit<Props, 'aberto'>) {
  const { usuario, ehVeterinario } = useAuth();

  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  // O veterinário logado assume o tratamento por padrão.
  const [veterinarioId, setVeterinarioId] = useState(ehVeterinario ? usuario?.veterinarioId ?? '' : '');
  const [dataInicio, setDataInicio] = useState(paraValorInputData(new Date()));
  const [objetivo, setObjetivo] = useState('');
  const [observacoes, setObservacoes] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    api
      .get<Veterinario[]>('/api/veterinarios')
      .then(({ data }) => setVeterinarios(data))
      .catch((falha) => setErro(mensagemDeErro(falha, 'Não foi possível carregar os veterinários.')));
  }, []);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (!objetivo.trim()) {
      setErro('Descreva o objetivo terapêutico do tratamento.');
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
        objetivoTerapeutico: objetivo,
        observacoesGerais: observacoes,
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
      <form onSubmit={aoEnviar} className="space-y-4">
        <Campo rotulo="Objetivo terapêutico" obrigatorio>
          <textarea
            className="vc-campo"
            rows={3}
            value={objetivo}
            onChange={(e) => setObjetivo(e.target.value)}
            placeholder="Ex.: recuperar a amplitude de movimento do membro posterior direito após cirurgia."
          />
        </Campo>

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo rotulo="Veterinário responsável" obrigatorio>
            <select
              className="vc-campo"
              value={veterinarioId}
              onChange={(e) => setVeterinarioId(e.target.value)}
              disabled={ehVeterinario}
            >
              <option value="">Selecione</option>
              {veterinarios.map((vet) => (
                <option key={vet.id} value={vet.id}>
                  {vet.nome}
                </option>
              ))}
            </select>
          </Campo>

          <Campo rotulo="Data de início" obrigatorio>
            <input type="date" className="vc-campo" value={dataInicio} onChange={(e) => setDataInicio(e.target.value)} />
          </Campo>
        </div>

        <Campo rotulo="Observações gerais">
          <textarea
            className="vc-campo"
            rows={2}
            value={observacoes}
            onChange={(e) => setObservacoes(e.target.value)}
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
