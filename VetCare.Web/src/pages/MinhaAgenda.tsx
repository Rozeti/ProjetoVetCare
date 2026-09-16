import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { CalendarDays, Check, Info, X } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useCarregamento } from '../hooks/useCarregamento';
import type { Sessao } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { estiloStatusSessao, formatarDataExtensa, formatarHora } from '../utils/formato';

/** HU-006 e HU-013, CA-3: agenda dos pets do tutor, com confirmação e cancelamento. */
export function MinhaAgenda() {
  const [apenasFuturas, setApenasFuturas] = useState(true);
  const [aviso, setAviso] = useState('');

  const buscar = useCallback(async () => {
    const { data } = await api.get<Sessao[]>('/api/sessoes/minhas', { params: { apenasFuturas } });
    return data;
  }, [apenasFuturas]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar sua agenda.',
  );

  const sessoes = dados ?? [];

  async function alterarStatus(sessao: Sessao, status: 'Confirmada' | 'Cancelada') {
    setErro('');
    setAviso('');

    try {
      await api.patch(`/api/sessoes/${sessao.id}/status`, { status });

      setAviso(
        status === 'Confirmada'
          ? `Presença confirmada para a sessão de ${sessao.nomePaciente}.`
          : `Sessão de ${sessao.nomePaciente} cancelada. A clínica foi avisada.`,
      );

      recarregar();
    } catch (falha) {
      // RN-009: cancelamento fora do prazo mínimo retorna a explicação da API.
      setErro(mensagemDeErro(falha, 'Não foi possível atualizar a sessão.'));
    }
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Minha agenda"
        descricao="Confirme ou cancele a presença do seu pet nas sessões agendadas."
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

      <Card className="mb-4 p-4">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            className="h-4 w-4 rounded accent-brand"
            checked={apenasFuturas}
            onChange={(e) => setApenasFuturas(e.target.checked)}
          />
          Mostrar apenas as próximas sessões
        </label>
      </Card>

      {carregando ? (
        <Carregando texto="Carregando sua agenda..." />
      ) : sessoes.length === 0 ? (
        <Card>
          <SemDados
            icone={<CalendarDays size={40} />}
            titulo="Nenhuma sessão agendada"
            descricao="Assim que a clínica marcar uma sessão para o seu pet, ela aparece aqui e você recebe uma notificação."
          />
        </Card>
      ) : (
        <ul className="space-y-4">
          {sessoes.map((sessao) => (
            <Card key={sessao.id} className="p-5">
              <li>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="mb-1 flex flex-wrap items-center gap-2">
                      <Link
                        to={`/prontuario/${sessao.pacienteId}`}
                        className="text-lg font-bold text-slate-900 hover:text-brand"
                      >
                        {sessao.nomePaciente}
                      </Link>
                      <Etiqueta className={estiloStatusSessao[sessao.status]}>{sessao.status}</Etiqueta>
                    </div>

                    <p className="text-sm capitalize text-slate-600">
                      {formatarDataExtensa(sessao.dataHora)} às {formatarHora(sessao.dataHora)}
                    </p>

                    {sessao.nomeVeterinario && (
                      <p className="mt-0.5 text-sm text-slate-500">Com {sessao.nomeVeterinario}</p>
                    )}
                  </div>

                  {sessao.status !== 'Cancelada' && sessao.status !== 'Concluída' && (
                    <div className="flex gap-2">
                      {sessao.status === 'Aguardando confirmação' && (
                        <button
                          type="button"
                          className="vc-botao bg-sucesso text-white hover:bg-emerald-700"
                          onClick={() => alterarStatus(sessao, 'Confirmada')}
                        >
                          <Check size={16} />
                          Confirmar presença
                        </button>
                      )}

                      <button
                        type="button"
                        className="vc-botao-secundario"
                        onClick={() => alterarStatus(sessao, 'Cancelada')}
                        disabled={!sessao.podeCancelar}
                        title={
                          sessao.podeCancelar
                            ? 'Cancelar presença'
                            : 'O prazo mínimo de cancelamento já passou. Entre em contato com a clínica.'
                        }
                      >
                        <X size={16} />
                        Cancelar
                      </button>
                    </div>
                  )}
                </div>

                {sessao.observacoes && (
                  <p className="mt-3 flex items-start gap-2 rounded-xl bg-brand-50 px-4 py-3 text-sm text-brand-dark">
                    <Info size={16} className="mt-0.5 shrink-0" />
                    {sessao.observacoes}
                  </p>
                )}

                {/* RN-009: a interface avisa antes de o tutor tentar uma ação que será recusada. */}
                {!sessao.podeCancelar && sessao.status === 'Aguardando confirmação' && (
                  <p className="mt-2 text-xs text-slate-500">
                    O prazo para cancelar esta sessão pelo aplicativo já passou. Fale com a clínica se precisar remarcar.
                  </p>
                )}
              </li>
            </Card>
          ))}
        </ul>
      )}
    </>
  );
}
