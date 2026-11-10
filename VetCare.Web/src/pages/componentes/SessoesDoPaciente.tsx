import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Activity, ListFilter } from 'lucide-react';
import type { Sessao, SituacaoSessao, Tratamento } from '../../types';
import { Card, SemDados } from '../../components/ui';
import { paraValorInputData } from '../../utils/formato';
import { ListaDeSessoes, type AcoesDaSessao } from './ListaDeSessoes';

const SITUACOES: SituacaoSessao[] = ['Agendada', 'Aguardando registro', 'Realizada', 'Cancelada'];

interface Props {
  pacienteId: string;
  /** Sessões cadastradas do animal, de todos os tratamentos. */
  sessoes: Sessao[];
  tratamentos: Tratamento[];
  acoesDaSessao: AcoesDaSessao;
  /** Atalho para a tela de sessões, só para quem tem acesso a ela. */
  exibeLinkDaTela: boolean;
}

/**
 * Aba Sessões do prontuário: as sessões cadastradas em todos os tratamentos do animal — as que
 * vão acontecer, as que aguardam o registro do atendimento e as realizadas —, com filtro por
 * tratamento, situação e período. As sessões são cadastradas no tratamento (aba Tratamentos).
 */
export function SessoesDoPaciente({ pacienteId, sessoes, tratamentos, acoesDaSessao, exibeLinkDaTela }: Props) {
  const [tratamentoId, setTratamentoId] = useState('');
  const [situacao, setSituacao] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');

  const filtrando = !!tratamentoId || !!situacao || !!de || !!ate;
  const periodoInvalido = !!de && !!ate && ate < de;
  const comSessoes = tratamentos.filter((t) => sessoes.some((s) => s.tratamentoId === t.id));

  const visiveis = sessoes.filter((sessao) => {
    const dia = paraValorInputData(sessao.dataHora);
    if (tratamentoId && sessao.tratamentoId !== tratamentoId) return false;
    if (situacao && sessao.situacao !== situacao) return false;
    if (de && dia < de) return false;
    if (ate && dia > ate) return false;
    return true;
  });

  function limpar() {
    setTratamentoId('');
    setSituacao('');
    setDe('');
    setAte('');
  }

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
        <div>
          <h2 className="font-semibold text-slate-900">Sessões</h2>
          <p className="text-xs text-slate-500">
            As sessões cadastradas nos tratamentos, com o registro do atendimento das já realizadas.
          </p>
        </div>

        {exibeLinkDaTela && sessoes.length > 0 && (
          <Link to={`/sessoes?pacienteId=${pacienteId}`} className="vc-botao-secundario">
            <ListFilter size={16} />
            Abrir na tela de sessões
          </Link>
        )}
      </div>

      {sessoes.length > 0 && (
        <div className="flex flex-wrap items-end gap-3 border-b border-slate-100 px-5 py-3">
          <label className="text-xs font-semibold text-slate-600">
            Tratamento
            <select className="vc-campo mt-1 block w-auto py-1.5" value={tratamentoId} onChange={(e) => setTratamentoId(e.target.value)}>
              <option value="">Todos</option>
              {comSessoes.map((tratamento) => (
                <option key={tratamento.id} value={tratamento.id}>
                  {tratamento.nome}
                </option>
              ))}
            </select>
          </label>
          <label className="text-xs font-semibold text-slate-600">
            Situação
            <select className="vc-campo mt-1 block w-auto py-1.5" value={situacao} onChange={(e) => setSituacao(e.target.value)}>
              <option value="">Todas</option>
              {SITUACOES.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
          </label>
          <label className="text-xs font-semibold text-slate-600">
            De
            <input type="date" className="vc-campo mt-1 block w-auto py-1.5" value={de} max={ate || undefined} onChange={(e) => setDe(e.target.value)} />
          </label>
          <label className="text-xs font-semibold text-slate-600">
            Até
            <input type="date" className="vc-campo mt-1 block w-auto py-1.5" value={ate} min={de || undefined} onChange={(e) => setAte(e.target.value)} />
          </label>

          {filtrando && (
            <button type="button" onClick={limpar} className="pb-2.5 text-xs font-medium text-brand hover:underline">
              Limpar filtros
            </button>
          )}
        </div>
      )}

      {periodoInvalido && <p className="px-5 pt-3 text-sm text-perigo">A data final não pode ser anterior à inicial.</p>}

      <div className="p-5">
        {sessoes.length === 0 ? (
          <SemDados
            icone={<Activity size={40} />}
            titulo="Nenhuma sessão cadastrada"
            descricao={
              tratamentos.length === 0
                ? 'As sessões são cadastradas dentro de um tratamento, e o tratamento nasce de uma avaliação.'
                : 'As sessões são cadastradas no tratamento, na aba Tratamentos, uma ou várias de uma vez.'
            }
            compacto
          />
        ) : (
          <ListaDeSessoes
            sessoes={visiveis}
            exibirTratamento
            vazio="Nenhuma sessão no filtro escolhido."
            {...acoesDaSessao}
          />
        )}
      </div>
    </Card>
  );
}
