import { useCallback, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { FileText, Loader2, PawPrint, Plus } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import type { Pet } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando, Modal, SemDados } from '../components/ui';
import { AlertasClinicos } from '../components/AlertasClinicos';
import { formatarData, formatarPeso, paraValorInputData } from '../utils/formato';

const FORM_VAZIO = {
  nome: '',
  especie: 'Cachorro',
  raca: '',
  sexo: 'Macho',
  pelagem: '',
  microchip: '',
  castrado: false,
  dataNascimento: '',
  pesoAtualKg: '',
};

/** HU-013, CA-1: o tutor vê apenas os pets sob sua responsabilidade. */
export function MeusPets() {
  const [aviso, setAviso] = useState('');
  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState(FORM_VAZIO);
  const [erroForm, setErroForm] = useState('');
  const [salvando, setSalvando] = useState(false);

  const buscar = useCallback(async () => {
    const { data } = await api.get<Pet[]>('/api/pets/meus');
    return data;
  }, []);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar seus pets.',
  );

  // A lista se refaz sozinha quando a clínica mexe no cadastro de um dos pets.
  useAtualizacao(['pets', 'alergias', 'vacinas'], recarregar);

  const pets = dados ?? [];

  function abrirCadastro() {
    setForm(FORM_VAZIO);
    setErroForm('');
    setModalAberto(true);
  }

  /**
   * Cadastro do animal recém-adquirido. O tutor responsável não aparece no formulário:
   * a API o deduz de quem está autenticado (RN-001).
   */
  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErroForm('');

    if (!form.nome.trim() || !form.dataNascimento) {
      setErroForm('Informe o nome e a data de nascimento do seu pet.');
      return;
    }

    setSalvando(true);

    try {
      const { data } = await api.post<Pet>('/api/pets/meus', {
        nome: form.nome,
        especie: form.especie,
        raca: form.raca,
        sexo: form.sexo,
        pelagem: form.pelagem,
        microchip: form.microchip,
        castrado: form.castrado,
        dataNascimento: form.dataNascimento,
        pesoAtualKg: form.pesoAtualKg ? Number(form.pesoAtualKg) : null,
      });

      setAviso(`${data.nome} foi cadastrado e já aparece para a equipe da clínica.`);
      setModalAberto(false);
      recarregar();
    } catch (falha) {
      setErroForm(mensagemDeErro(falha, 'Não foi possível cadastrar seu pet.'));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Meus pets"
        descricao="Acompanhe o tratamento e a evolução clínica dos seus animais."
        acoes={
          <button type="button" className="vc-botao-primario" onClick={abrirCadastro}>
            <Plus size={16} />
            Cadastrar pet
          </button>
        }
      />

      {erro && (
        <div className="mb-4">
          <Alerta tipo="erro" aoFechar={() => setErro('')}>
            {erro}
          </Alerta>
        </div>
      )}
      {aviso && (
        <div className="mb-4">
          <Alerta tipo="sucesso" aoFechar={() => setAviso('')}>
            {aviso}
          </Alerta>
        </div>
      )}

      {carregando ? (
        <Carregando texto="Carregando seus pets..." />
      ) : pets.length === 0 ? (
        <Card>
          <SemDados
            icone={<PawPrint size={40} />}
            titulo="Nenhum pet cadastrado"
            descricao="Cadastre aqui o animal que acabou de chegar à família. Pets cadastrados pela clínica também aparecem nesta lista."
            acao={
              <button type="button" className="vc-botao-sutil" onClick={abrirCadastro}>
                <Plus size={16} />
                Cadastrar meu pet
              </button>
            }
          />
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {pets.map((pet) => (
            <Card key={pet.id} className="p-5">
              <div className="mb-4 flex items-center gap-4">
                <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand-100 text-brand">
                  <PawPrint size={26} />
                </div>

                <div className="min-w-0">
                  <h2 className="truncate text-lg font-bold text-slate-900">{pet.nome}</h2>
                  <p className="truncate text-sm text-slate-500">
                    {pet.especie}
                    {pet.raca && ` · ${pet.raca}`}
                  </p>
                </div>
              </div>

              {pet.alertasClinicos.length > 0 && (
                <div className="mb-4">
                  <AlertasClinicos alertas={pet.alertasClinicos} compacto />
                </div>
              )}

              <dl className="mb-4 grid grid-cols-3 gap-2 rounded-xl bg-slate-50 p-3 text-center">
                <div>
                  <dt className="text-xs text-slate-500">Idade</dt>
                  <dd className="text-sm font-semibold text-slate-800">{pet.idadeDescritiva}</dd>
                </div>
                <div>
                  <dt className="text-xs text-slate-500">Peso</dt>
                  <dd className="text-sm font-semibold text-slate-800">{formatarPeso(pet.pesoAtualKg)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-slate-500">Nascimento</dt>
                  <dd className="text-sm font-semibold text-slate-800">{formatarData(pet.dataNascimento)}</dd>
                </div>
              </dl>

              {/* HU-013, CA-2: a consulta abre a visão filtrada do prontuário. */}
              <Link to={`/prontuario/${pet.id}`} className="vc-botao-primario w-full">
                <FileText size={16} />
                Ver prontuário
              </Link>
            </Card>
          ))}
        </div>
      )}

      <Modal
        aberto={modalAberto}
        titulo="Cadastrar meu pet"
        descricao="Preencha o que souber. A equipe completa o cadastro na primeira consulta."
        aoFechar={() => setModalAberto(false)}
      >
        <form onSubmit={aoEnviar} className="space-y-4">
          <Campo rotulo="Nome do pet" obrigatorio>
            <input
              className="vc-campo"
              value={form.nome}
              onChange={(e) => setForm({ ...form, nome: e.target.value })}
              placeholder="Ex.: Thor"
            />
          </Campo>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Espécie" obrigatorio>
              <select
                className="vc-campo"
                value={form.especie}
                onChange={(e) => setForm({ ...form, especie: e.target.value })}
              >
                <option>Cachorro</option>
                <option>Gato</option>
                <option>Ave</option>
                <option>Roedor</option>
                <option>Outro</option>
              </select>
            </Campo>

            <Campo rotulo="Raça">
              <input
                className="vc-campo"
                value={form.raca}
                onChange={(e) => setForm({ ...form, raca: e.target.value })}
                placeholder="Deixe em branco se não souber"
              />
            </Campo>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <Campo rotulo="Sexo">
              <select
                className="vc-campo"
                value={form.sexo}
                onChange={(e) => setForm({ ...form, sexo: e.target.value })}
              >
                <option>Macho</option>
                <option>Fêmea</option>
              </select>
            </Campo>

            <Campo rotulo="Nascimento" obrigatorio dica="Se não souber a data exata, use a aproximada.">
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

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Pelagem">
              <input
                className="vc-campo"
                value={form.pelagem}
                onChange={(e) => setForm({ ...form, pelagem: e.target.value })}
                placeholder="Ex.: caramelo curto"
              />
            </Campo>

            <Campo rotulo="Microchip" dica="Número da identificação eletrônica, se o animal tiver.">
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
            Meu pet é castrado
          </label>

          {erroForm && <Alerta tipo="erro">{erroForm}</Alerta>}

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              className="vc-botao-secundario"
              onClick={() => setModalAberto(false)}
              disabled={salvando}
            >
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              Cadastrar pet
            </button>
          </div>
        </form>
      </Modal>
    </>
  );
}
