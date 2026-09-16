import { useCallback, useState } from 'react';
import { Download, ScrollText, Search } from 'lucide-react';
import { api } from '../services/api';
import type { PaginaDe, RegistroAuditoria } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { baixarCsv } from '../utils/impressao';
import { formatarDataHora, paraValorInputData } from '../utils/formato';
import { useCarregamento } from '../hooks/useCarregamento';
import { paginaVazia } from '../utils/paginacao';

const ACOES = [
  'Login',
  'LoginNegado',
  'Consulta',
  'Criacao',
  'Alteracao',
  'Inativacao',
  'Download',
  'RedefinicaoSenha',
];

const ESTILO_ACAO: Record<string, string> = {
  Login: 'bg-sucesso-claro text-emerald-800',
  LoginNegado: 'bg-perigo-claro text-red-800',
  Consulta: 'bg-brand-100 text-brand-dark',
  Criacao: 'bg-info-claro text-purple-800',
  Alteracao: 'bg-alerta-claro text-amber-800',
  Inativacao: 'bg-slate-200 text-slate-700',
  Download: 'bg-slate-100 text-slate-600',
  RedefinicaoSenha: 'bg-alerta-claro text-amber-800',
};

function trintaDiasAtras(): string {
  const data = new Date();
  data.setDate(data.getDate() - 29);
  return paraValorInputData(data);
}

/**
 * Trilha de auditoria da clínica. Registrar quem consultou cada prontuário é
 * exigência de rastreabilidade em dados clínicos e base para responder a
 * pedidos de titulares sobre o uso dos seus dados.
 */
export function Auditoria() {
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [acao, setAcao] = useState('');
  const [inicio, setInicio] = useState(trintaDiasAtras);
  const [fim, setFim] = useState(paraValorInputData(new Date()));

  const buscar = useCallback(async () => {
    const { data } = await api.get<PaginaDe<RegistroAuditoria>>('/api/auditoria', {
      params: {
        acao: acao || undefined,
        inicio,
        fim,
        pagina: numeroPagina,
        tamanho: 25,
      },
    });

    return data;
  }, [acao, inicio, fim, numeroPagina]);

  const { dados, carregando, erro, setErro, recarregar } = useCarregamento(
    buscar,
    'Não foi possível carregar a trilha de auditoria.',
  );

  const pagina = dados ?? paginaVazia<RegistroAuditoria>(25);

  // Um filtro novo invalida a página atual: o resultado pode ter menos páginas.
  function filtrar(aplicar: () => void) {
    aplicar();
    setNumeroPagina(1);
  }

  /** Exporta a página carregada para auditorias externas. */
  function exportar() {
    baixarCsv(
      `auditoria-${inicio}-a-${fim}`,
      ['Data e hora', 'Usuário', 'Perfil', 'Ação', 'Entidade', 'Detalhe', 'Endereço IP'],
      pagina.itens.map((r) => [
        formatarDataHora(r.dataHora),
        r.nomeUsuario,
        r.perfil,
        r.acao,
        r.entidade,
        r.detalhe,
        r.enderecoIp,
      ]),
    );
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Trilha de auditoria"
        descricao="Registro de acessos e alterações nos dados clínicos da clínica."
        acoes={
          pagina.itens.length > 0 && (
            <button type="button" className="vc-botao-secundario" onClick={exportar}>
              <Download size={16} />
              Exportar página
            </button>
          )
        }
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
            <label htmlFor="acao" className="vc-rotulo">
              Ação
            </label>
            <select id="acao" className="vc-campo w-auto" value={acao} onChange={(e) => filtrar(() => setAcao(e.target.value))}>
              <option value="">Todas</option>
              {ACOES.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="inicio" className="vc-rotulo">
              De
            </label>
            <input
              id="inicio"
              type="date"
              className="vc-campo w-auto"
              value={inicio}
              max={fim}
              onChange={(e) => filtrar(() => setInicio(e.target.value))}
            />
          </div>

          <div>
            <label htmlFor="fim" className="vc-rotulo">
              Até
            </label>
            <input
              id="fim"
              type="date"
              className="vc-campo w-auto"
              value={fim}
              min={inicio}
              max={paraValorInputData(new Date())}
              onChange={(e) => filtrar(() => setFim(e.target.value))}
            />
          </div>

          <button type="button" className="vc-botao-secundario" onClick={recarregar}>
            <Search size={16} />
            Filtrar
          </button>
        </div>
      </Card>

      {carregando ? (
        <Carregando texto="Carregando registros..." />
      ) : pagina.itens.length === 0 ? (
        <Card>
          <SemDados
            icone={<ScrollText size={40} />}
            titulo="Nenhum registro no período"
            descricao="Acessos a prontuários, alterações de cadastro e tentativas de login aparecem aqui."
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th>Data e hora</th>
                  <th>Usuário</th>
                  <th>Ação</th>
                  <th>Entidade</th>
                  <th>Detalhe</th>
                  <th>Origem</th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((registro) => (
                  <tr key={registro.id}>
                    <td className="whitespace-nowrap text-xs">{formatarDataHora(registro.dataHora)}</td>
                    <td>
                      <span className="block font-medium text-slate-900">{registro.nomeUsuario || '—'}</span>
                      <span className="text-xs text-slate-400">{registro.perfil}</span>
                    </td>
                    <td>
                      <Etiqueta className={ESTILO_ACAO[registro.acao] ?? 'bg-slate-100 text-slate-600'}>
                        {registro.acao}
                      </Etiqueta>
                    </td>
                    <td className="text-xs text-slate-500">{registro.entidade || '—'}</td>
                    <td className="text-xs text-slate-600">{registro.detalhe || '—'}</td>
                    <td className="text-xs text-slate-400">{registro.enderecoIp || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Paginacao pagina={pagina} aoMudarPagina={setNumeroPagina} rotuloItens="registros" />
        </Card>
      )}
    </>
  );
}
