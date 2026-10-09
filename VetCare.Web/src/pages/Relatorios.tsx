import { useCallback, useEffect, useRef, useState } from 'react';
import { Activity, BarChart3, ClipboardList, HelpCircle, Loader2, Users } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAtualizacao } from '../contexts/atualizacoes';
import type { RelatorioProdutividade } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Estatistica, SemDados } from '../components/ui';
import { formatarData, paraValorInputData, trintaDiasAtras } from '../utils/formato';
import { ModalComoGerarRelatorio } from './componentes/ModalComoGerarRelatorio';

interface Periodo {
  inicio: string;
  fim: string;
}

/** HU-017: relatórios de produtividade por período. */
export function Relatorios() {
  const [inicio, setInicio] = useState(trintaDiasAtras);
  const [fim, setFim] = useState(paraValorInputData(new Date()));
  const [erroPeriodo, setErroPeriodo] = useState('');

  const [relatorio, setRelatorio] = useState<RelatorioProdutividade | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');
  // Relatório sem atendimentos gerado pelo botão: abre a janela que explica o que falta registrar.
  const [relatorioSemDados, setRelatorioSemDados] = useState<RelatorioProdutividade | null>(null);

  // O relatório exibido segue o último período gerado, não o que está sendo digitado nos campos.
  const periodoGerado = useRef<Periodo>({ inicio, fim });
  // Só a resposta da busca mais recente vale: uma resposta lenta não sobrescreve a seguinte.
  const ultimaBusca = useRef(0);

  const carregar = useCallback(
    async (periodo: Periodo, { pedidoDoUsuario = false, silencioso = false } = {}) => {
      const busca = ++ultimaBusca.current;
      periodoGerado.current = periodo;

      if (!silencioso) setCarregando(true);

      try {
        const { data } = await api.get<RelatorioProdutividade>('/api/dashboard/relatorio-produtividade', {
          params: periodo,
        });

        if (busca !== ultimaBusca.current) return;

        setRelatorio(data);
        setErro('');

        // Sem atendimentos, os quadros por veterinário e de técnicas ficam vazios: quem
        // clicou em "Gerar relatório" é orientado sobre o que precisa ser registrado antes.
        if (pedidoDoUsuario && data.totalAtendimentos === 0) {
          setRelatorioSemDados(data);
        }
      } catch (falha) {
        if (busca === ultimaBusca.current) {
          setErro(mensagemDeErro(falha, 'Não foi possível gerar o relatório.'));
        }
      } finally {
        if (busca === ultimaBusca.current) setCarregando(false);
      }
    },
    [],
  );

  // Ao abrir a tela, o relatório dos últimos 30 dias já aparece.
  useEffect(() => {
    carregar(periodoGerado.current);
  }, [carregar]);

  // Um atendimento registrado agora entra no relatório do período sem recarregar a página.
  useAtualizacao(['atendimentos', 'avaliacoes'], () => carregar(periodoGerado.current, { silencioso: true }));

  const fecharAjuda = useCallback(() => setRelatorioSemDados(null), []);

  /** Os campos de data ficam fora de um <form>: a ordem do período é conferida aqui. */
  function gerar() {
    setErroPeriodo('');

    if (!inicio || !fim) {
      setErroPeriodo('Informe o início e o fim do período.');
      return;
    }

    if (inicio > fim) {
      setErroPeriodo('O início do período não pode ser posterior ao fim.');
      return;
    }

    if (fim > paraValorInputData(new Date())) {
      setErroPeriodo('O fim do período não pode estar no futuro.');
      return;
    }

    carregar({ inicio, fim }, { pedidoDoUsuario: true });
  }

  const maiorOcorrencia = relatorio?.tecnicasMaisAplicadas[0]?.ocorrencias ?? 0;
  const maiorTotalVet = relatorio?.porVeterinario[0]?.totalAtendimentos ?? 0;

  return (
    <>
      <CabecalhoPagina
        titulo="Relatórios de produtividade"
        descricao="Desempenho dos veterinários e técnicas mais aplicadas no período."
      />

      {erro && (
        <div className="mb-4">
          <Alerta tipo="erro" aoFechar={() => setErro('')}>
            {erro}
          </Alerta>
        </div>
      )}

      <Card className="mb-6 p-4">
        <div className="flex flex-wrap items-end gap-4">
          <div>
            <label htmlFor="inicio" className="vc-rotulo">
              Início do período
            </label>
            <input
              id="inicio"
              type="date"
              className="vc-campo w-auto"
              value={inicio}
              max={fim}
              onChange={(e) => setInicio(e.target.value)}
            />
          </div>

          <div>
            <label htmlFor="fim" className="vc-rotulo">
              Fim do período
            </label>
            <input
              id="fim"
              type="date"
              className="vc-campo w-auto"
              value={fim}
              min={inicio}
              max={paraValorInputData(new Date())}
              onChange={(e) => setFim(e.target.value)}
            />
          </div>

          <button type="button" className="vc-botao-primario" onClick={gerar} disabled={carregando}>
            {carregando ? <Loader2 className="animate-spin" size={16} /> : <BarChart3 size={16} />}
            Gerar relatório
          </button>
        </div>

        {erroPeriodo && (
          <div className="mt-3">
            <Alerta tipo="erro" aoFechar={() => setErroPeriodo('')}>
              {erroPeriodo}
            </Alerta>
          </div>
        )}
      </Card>

      {carregando ? (
        <Carregando texto="Gerando relatório..." />
      ) : !relatorio ? null : relatorio.semRegistros ? (
        /* HU-017, CA-3: período sem dados informa a ausência de registros. */
        <Card>
          <SemDados
            icone={<BarChart3 size={40} />}
            titulo="Nenhum registro no período selecionado"
            descricao={`Não há avaliações nem atendimentos entre ${formatarData(relatorio.inicio)} e ${formatarData(
              relatorio.fim,
            )}. Escolha outro intervalo de datas.`}
            acao={
              <button type="button" className="vc-botao-sutil" onClick={() => setRelatorioSemDados(relatorio)}>
                <HelpCircle size={16} />
                Como gerar dados para o relatório
              </button>
            }
          />
        </Card>
      ) : (
        <>
          <div className="mb-6 grid gap-4 sm:grid-cols-3">
            <Estatistica
              rotulo="Atendimentos no período"
              valor={relatorio.totalAtendimentos}
              icone={<Activity size={20} />}
            />
            <Estatistica
              rotulo="Avaliações no período"
              valor={relatorio.totalAvaliacoes}
              icone={<ClipboardList size={20} />}
              cor="text-info"
              fundo="bg-info-claro"
            />
            <Estatistica
              rotulo="Média da escala de dor"
              valor={relatorio.mediaEscalaDor.toFixed(1).replace('.', ',')}
              icone={<BarChart3 size={20} />}
              cor="text-alerta"
              fundo="bg-alerta-claro"
              destaque="Quanto menor, melhor a resposta ao tratamento"
            />
          </div>

          <div className="grid gap-6 lg:grid-cols-2">
            {/* HU-017, CA-1: total de atendimentos por veterinário. */}
            <Card>
              <div className="border-b border-slate-200 px-5 py-4">
                <h2 className="font-semibold text-slate-900">Atendimentos por veterinário</h2>
              </div>

              {relatorio.porVeterinario.length === 0 ? (
                <SemDados icone={<Users size={36} />} titulo="Sem atendimentos no período" />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {relatorio.porVeterinario.map((item) => (
                    <li key={item.veterinarioId} className="px-5 py-4">
                      <div className="mb-2 flex items-baseline justify-between gap-3">
                        <p className="font-medium text-slate-900">{item.nome}</p>
                        <span className="text-sm text-slate-600">
                          <strong className="text-slate-900">{item.totalAtendimentos}</strong> atendimento(s)
                        </span>
                      </div>

                      <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                        <div
                          className="h-full rounded-full bg-brand"
                          style={{ width: `${maiorTotalVet ? (item.totalAtendimentos / maiorTotalVet) * 100 : 0}%` }}
                        />
                      </div>

                      <p className="mt-1 text-xs text-slate-500">
                        Escala de dor média: {item.mediaEscalaDor.toFixed(1).replace('.', ',')}/10
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* HU-017, CA-2: ranking das técnicas mais aplicadas. */}
            <Card>
              <div className="border-b border-slate-200 px-5 py-4">
                <h2 className="font-semibold text-slate-900">Técnicas mais aplicadas</h2>
              </div>

              {relatorio.tecnicasMaisAplicadas.length === 0 ? (
                <SemDados icone={<Activity size={36} />} titulo="Nenhuma técnica registrada no período" />
              ) : (
                <ol className="divide-y divide-slate-100">
                  {relatorio.tecnicasMaisAplicadas.map((tecnica, indice) => (
                    <li key={tecnica.tecnica} className="flex items-center gap-4 px-5 py-3">
                      <span className="w-6 shrink-0 text-sm font-bold text-slate-400">{indice + 1}º</span>

                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium text-slate-800">{tecnica.tecnica}</p>
                        <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-slate-100">
                          <div
                            className="h-full rounded-full bg-info"
                            style={{ width: `${maiorOcorrencia ? (tecnica.ocorrencias / maiorOcorrencia) * 100 : 0}%` }}
                          />
                        </div>
                      </div>

                      <span className="shrink-0 text-sm font-semibold text-slate-700">{tecnica.ocorrencias}x</span>
                    </li>
                  ))}
                </ol>
              )}
            </Card>
          </div>

          <p className="mt-6 text-center text-xs text-slate-400">
            Período de {formatarData(relatorio.inicio)} a {formatarData(relatorio.fim)}
          </p>
        </>
      )}

      <ModalComoGerarRelatorio relatorio={relatorioSemDados} aoFechar={fecharAjuda} />
    </>
  );
}
