import { useEffect, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import { ESPECIE_ATENDIDA, RACAS_DE_CAES, type Pet, type Tutor, type Veterinario } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { paraValorInputData, paraValorInputDia, validarDadosDoPet } from '../../utils/formato';

const FORM_VAZIO = {
  nome: '',
  especie: ESPECIE_ATENDIDA,
  raca: '',
  sexo: 'Macho',
  pelagem: '',
  microchip: '',
  castrado: false,
  dataNascimento: '',
  pesoAtualKg: '',
  tutorId: '',
  veterinarioResponsavelId: '',
  dataObito: '',
};

interface Props {
  aberto: boolean;
  /** Paciente em edição; sem ele o modal cadastra um paciente novo. */
  pet?: Pet | null;
  /** Valores sugeridos no cadastro (vindos dos filtros da lista). */
  tutorInicial?: string;
  veterinarioInicial?: string;
  aoFechar: () => void;
  aoSalvar: (mensagem: string) => void;
}

/**
 * HU-003: cadastro e edição do paciente. O cadastro parte da lista de pacientes; a edição,
 * do prontuário do animal. Todo paciente tem um veterinário responsável, escolhido aqui e
 * trocado na edição; qualquer veterinário da clínica pode atendê-lo.
 */
export function ModalCadastroPaciente({ aberto, ...props }: Props) {
  // O formulário só existe enquanto o modal está aberto: cada abertura monta campos novos.
  if (!aberto) return null;

  return <Formulario {...props} />;
}

function Formulario({ pet, tutorInicial = '', veterinarioInicial = '', aoFechar, aoSalvar }: Omit<Props, 'aberto'>) {
  const { usuario, ehVeterinario } = useAuth();
  const { confirmarEdicao } = useConfirmacao();

  const [form, setForm] = useState(() =>
    pet
      ? {
          nome: pet.nome,
          especie: pet.especie,
          raca: pet.raca,
          sexo: pet.sexo || 'Macho',
          pelagem: pet.pelagem,
          microchip: pet.microchip,
          castrado: pet.castrado,
          dataNascimento: paraValorInputDia(pet.dataNascimento),
          pesoAtualKg: pet.pesoAtualKg?.toString() ?? '',
          tutorId: pet.tutorId,
          veterinarioResponsavelId: pet.veterinarioResponsavelId ?? '',
          dataObito: pet.dataObito ? paraValorInputDia(pet.dataObito) : '',
        }
      : {
          ...FORM_VAZIO,
          tutorId: tutorInicial,
          // O veterinário que cadastra vem sugerido como responsável; ele pode escolher um colega.
          veterinarioResponsavelId: veterinarioInicial || (ehVeterinario ? usuario?.veterinarioId ?? '' : ''),
        },
  );
  const [tutores, setTutores] = useState<Tutor[]>([]);
  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    let ativo = true;

    Promise.all([
      api.get<Tutor[]>('/api/tutores/selecao'),
      api.get<Veterinario[]>('/api/veterinarios'),
    ])
      .then(([respostaTutores, respostaVeterinarios]) => {
        if (!ativo) return;
        setTutores(respostaTutores.data);
        // Só profissionais ativos assumem pacientes; o responsável atual aparece mesmo se não estiver.
        setVeterinarios(
          respostaVeterinarios.data.filter((v) => v.ativo || v.id === pet?.veterinarioResponsavelId),
        );
      })
      .catch((falha) => {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os tutores e veterinários.'));
      });

    return () => {
      ativo = false;
    };
  }, [pet]);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    // HU-003, CA-2 / RN-001: o tutor é obrigatório e sinalizado antes do envio.
    if (!form.tutorId) {
      setErro('Selecione o tutor responsável pelo paciente.');
      return;
    }

    // Todo paciente precisa estar ligado a um veterinário responsável.
    if (!form.veterinarioResponsavelId) {
      setErro('Selecione o veterinário responsável pelo paciente.');
      return;
    }

    // As mesmas regras da API (CriarPetDTO / AtualizarPetDTO), conferidas antes do envio.
    const problema = validarDadosDoPet(form);

    if (problema) {
      setErro(problema);
      return;
    }

    if (form.dataObito && form.dataObito < form.dataNascimento) {
      setErro('A data de óbito não pode ser anterior à data de nascimento.');
      return;
    }

    // O óbito inativa o paciente e encerra os tratamentos: uma data futura faria isso antes da hora.
    if (form.dataObito && form.dataObito > paraValorInputData(new Date())) {
      setErro('A data de óbito não pode ser futura.');
      return;
    }

    if (
      pet &&
      !(await confirmarEdicao(
        <>
          Salvar as alterações no cadastro de <strong>{pet.nome}</strong>?
          {form.dataObito && !pet.dataObito && (
            <> O registro de óbito inativa o paciente e os tratamentos em aberto.</>
          )}
          {form.veterinarioResponsavelId !== (pet.veterinarioResponsavelId ?? '') && (
            <> O novo veterinário responsável e o tutor serão avisados.</>
          )}
        </>,
      ))
    ) {
      return;
    }

    setSalvando(true);

    const corpo = {
      nome: form.nome,
      especie: form.especie,
      raca: form.raca,
      sexo: form.sexo,
      pelagem: form.pelagem,
      microchip: form.microchip,
      castrado: form.castrado,
      dataNascimento: form.dataNascimento,
      pesoAtualKg: form.pesoAtualKg ? Number(form.pesoAtualKg) : null,
      tutorId: form.tutorId,
      veterinarioResponsavelId: form.veterinarioResponsavelId,
    };

    try {
      if (pet) {
        // A edição envia a data de óbito junto: omiti-la apagaria o registro existente.
        await api.put(`/api/pets/${pet.id}`, { ...corpo, dataObito: form.dataObito || null });
        aoSalvar('Paciente atualizado com sucesso.');
      } else {
        await api.post('/api/pets', corpo);
        aoSalvar('Paciente cadastrado com sucesso.');
      }
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível salvar o paciente.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal
      aberto
      titulo={pet ? 'Editar paciente' : 'Novo paciente'}
      descricao={
        pet
          ? 'A alteração preserva todo o histórico clínico já registrado.'
          : 'O paciente precisa estar vinculado a um tutor e a um veterinário responsável.'
      }
      aoFechar={aoFechar}
    >
      <form onSubmit={aoEnviar} className="space-y-4">
        <Campo rotulo="Nome do paciente" obrigatorio>
          <input
            className="vc-campo"
            value={form.nome}
            onChange={(e) => setForm({ ...form, nome: e.target.value })}
            placeholder="Ex.: Thor"
          />
        </Campo>

        <Campo rotulo="Raça" dica="A clínica atende apenas cães. Escolha uma sugestão ou digite a raça.">
          <input
            className="vc-campo"
            list="racas-de-caes-cadastro"
            value={form.raca}
            onChange={(e) => setForm({ ...form, raca: e.target.value })}
            placeholder="Ex.: SRD, Labrador Retriever, Shih Tzu"
            maxLength={80}
          />
        </Campo>
        <datalist id="racas-de-caes-cadastro">
          {RACAS_DE_CAES.map((raca) => (
            <option key={raca} value={raca} />
          ))}
        </datalist>

        {/* Pacientes antigos de outra espécie continuam editáveis, sem trocar de espécie. */}
        {pet && pet.especie !== ESPECIE_ATENDIDA && (
          <p className="rounded-xl bg-alerta-claro px-4 py-2 text-sm text-amber-800">
            Espécie registrada: <strong>{pet.especie}</strong>. Cadastro anterior à regra de atender só cães; a espécie
            fica como está.
          </p>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo rotulo="Pelagem">
            <input
              className="vc-campo"
              value={form.pelagem}
              onChange={(e) => setForm({ ...form, pelagem: e.target.value })}
              placeholder="Ex.: caramelo curto"
            />
          </Campo>

          <Campo rotulo="Microchip" dica="Não pode se repetir na clínica.">
            <input
              className="vc-campo"
              value={form.microchip}
              onChange={(e) => setForm({ ...form, microchip: e.target.value })}
            />
          </Campo>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            className="h-4 w-4 rounded accent-brand"
            checked={form.castrado}
            onChange={(e) => setForm({ ...form, castrado: e.target.checked })}
          />
          Paciente castrado
        </label>

        <div className="grid gap-4 sm:grid-cols-3">
          <Campo rotulo="Sexo">
            <select className="vc-campo" value={form.sexo} onChange={(e) => setForm({ ...form, sexo: e.target.value })}>
              <option>Macho</option>
              <option>Fêmea</option>
            </select>
          </Campo>

          <Campo rotulo="Nascimento" obrigatorio>
            <input
              type="date"
              className="vc-campo"
              max={paraValorInputData(new Date())}
              value={form.dataNascimento}
              onChange={(e) => setForm({ ...form, dataNascimento: e.target.value })}
            />
          </Campo>

          <Campo rotulo="Peso (kg)">
            <input
              type="number"
              step="0.1"
              min="0.1"
              className="vc-campo"
              value={form.pesoAtualKg}
              onChange={(e) => setForm({ ...form, pesoAtualKg: e.target.value })}
            />
          </Campo>
        </div>

        <Campo rotulo="Tutor responsável" obrigatorio dica="Cada paciente pertence a um único tutor.">
          <select className="vc-campo" value={form.tutorId} onChange={(e) => setForm({ ...form, tutorId: e.target.value })}>
            <option value="">Selecione o tutor</option>
            {/* Na edição o tutor atual aparece mesmo antes de a lista chegar. */}
            {pet && !tutores.some((t) => t.id === pet.tutorId) && <option value={pet.tutorId}>{pet.nomeTutor}</option>}
            {tutores.map((tutor) => (
              <option key={tutor.id} value={tutor.id}>
                {tutor.nome} {tutor.telefone && `— ${tutor.telefone}`}
              </option>
            ))}
          </select>
        </Campo>

        {/* Todo paciente tem um responsável, escolhido aqui e trocado na edição. Ele não restringe
            o acesso: qualquer veterinário da clínica atende o paciente. */}
        <Campo
          rotulo="Veterinário responsável"
          obrigatorio
          dica={
            pet
              ? 'Trocar o responsável não move tratamentos nem sessões: qualquer veterinário da clínica pode atender o paciente.'
              : 'Qualquer veterinário da clínica pode atender o paciente; o responsável organiza o acompanhamento.'
          }
        >
          <select
            className="vc-campo"
            value={form.veterinarioResponsavelId}
            onChange={(e) => setForm({ ...form, veterinarioResponsavelId: e.target.value })}
          >
            <option value="">Selecione o veterinário</option>
            {/* Na edição o responsável atual aparece mesmo antes de a lista chegar. */}
            {pet?.veterinarioResponsavelId && !veterinarios.some((v) => v.id === pet.veterinarioResponsavelId) && (
              <option value={pet.veterinarioResponsavelId}>{pet.nomeVeterinarioResponsavel}</option>
            )}
            {veterinarios.map((vet) => (
              <option key={vet.id} value={vet.id}>
                {vet.nome}
                {vet.especialidade && ` — ${vet.especialidade}`}
                {!vet.ativo && ' (desativado)'}
              </option>
            ))}
          </select>
        </Campo>

        {pet && (
          <Campo
            rotulo="Data de óbito"
            dica="Registrar o óbito inativa o paciente e os tratamentos em aberto. Deixe em branco se não se aplica."
          >
            <input
              type="date"
              className="vc-campo"
              min={form.dataNascimento || undefined}
              max={paraValorInputData(new Date())}
              value={form.dataObito}
              onChange={(e) => setForm({ ...form, dataObito: e.target.value })}
            />
          </Campo>
        )}

        {erro && <Alerta tipo="erro">{erro}</Alerta>}

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando && <Loader2 className="animate-spin" size={16} />}
            {pet ? 'Salvar alterações' : 'Cadastrar paciente'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
