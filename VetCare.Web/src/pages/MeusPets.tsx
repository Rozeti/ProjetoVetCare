import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { FileText, PawPrint } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import type { Pet } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, SemDados } from '../components/ui';
import { AlertasClinicos } from '../components/AlertasClinicos';
import { formatarData, formatarPeso } from '../utils/formato';

/** HU-013, CA-1: o tutor vê apenas os pets sob sua responsabilidade. */
export function MeusPets() {
  const [pets, setPets] = useState<Pet[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');

  useEffect(() => {
    api
      .get<Pet[]>('/api/pets/meus')
      .then(({ data }) => setPets(data))
      .catch((falha) => setErro(mensagemDeErro(falha, 'Não foi possível carregar seus pets.')))
      .finally(() => setCarregando(false));
  }, []);

  return (
    <>
      <CabecalhoPagina
        titulo="Meus pets"
        descricao="Acompanhe o tratamento e a evolução clínica dos seus animais."
      />

      {erro && (
        <div className="mb-4">
          <Alerta tipo="erro">{erro}</Alerta>
        </div>
      )}

      {carregando ? (
        <Carregando texto="Carregando seus pets..." />
      ) : pets.length === 0 ? (
        <Card>
          <SemDados
            icone={<PawPrint size={40} />}
            titulo="Nenhum pet cadastrado"
            descricao="Quando a clínica cadastrar um pet sob sua responsabilidade, ele aparecerá aqui."
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
    </>
  );
}
