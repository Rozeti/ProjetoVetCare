import { useCallback, useEffect, useState } from 'react';
import { Download, ScrollText, Search } from 'lucide-react';
import { api } from '../services/api';
import type { PaginaDe, RegistroAuditoria, Usuario } from '../types';
import { Alerta, CabecalhoPagina, Card, Carregando, Etiqueta, SemDados } from '../components/ui';
import { Paginacao } from '../components/Paginacao';
import { baixarCsv } from '../utils/impressao';
import { formatarDataHora, paraValorInputData, trintaDiasAtras } from '../utils/formato';
import { useCarregamento } from '../hooks/useCarregamento';
import { paginaVazia } from '../utils/paginacao';

/**
 * Ações mostradas na tela (AuditoriaService.Acoes.AlteracoesDeDados, na API). Logins,
 * consultas e downloads continuam gravados no banco, mas não aparecem aqui.
 */
const ACOES: { valor: string; rotulo: string; estilo: string }[] = [
  { valor: 'Criacao', rotulo: 'Criação', estilo: 'bg-info-claro text-purple-800' },
  { valor: 'Alteracao', rotulo: 'Alteração', estilo: 'bg-alerta-claro text-amber-800' },
  { valor: 'Inativacao', rotulo: 'Inativação ou cancelamento', estilo: 'bg-slate-200 text-slate-700' },
  { valor: 'Exclusao', rotulo: 'Exclusão', estilo: 'bg-perigo-claro text-red-800' },
];

/** Cadastros e registros que aparecem na trilha, com o nome que o usuário reconhece. */
const ENTIDADES: { valor: string; rotulo: string }[] = [
  { valor: 'Paciente', rotulo: 'Paciente' },
  { valor: 'Tutor', rotulo: 'Tutor' },
  { valor: 'Usuario', rotulo: 'Usuário' },
  { valor: 'Veterinario', rotulo: 'Veterinário' },
  { valor: 'AvaliacaoClinica', rotulo: 'Avaliação' },
  { valor: 'Tratamento', rotulo: 'Tratamento' },
  { valor: 'AtendimentoFisioterapeutico', rotulo: 'Evolução' },
  { valor: 'Sessao', rotulo: 'Sessão' },
  { valor: 'Vacina', rotulo: 'Vacina' },
  { valor: 'Prescricao', rotulo: 'Receita' },
  { valor: 'PedidoExame', rotulo: 'Pedido de exame' },
  { valor: 'AlergiaCondicao', rotulo: 'Alerta clínico' },
  { valor: 'ObservacaoInterna', rotulo: 'Observação interna' },
  { valor: 'DocumentoClinico', rotulo: 'Documento' },
  { valor: 'MidiaSessao', rotulo: 'Foto ou vídeo' },
  { valor: 'Clinica', rotulo: 'Configurações da clínica' },
];

const rotuloDaAcao = (acao: string) => ACOES.find((a) => a.valor === acao)?.rotulo ?? acao;
const rotuloDaEntidade = (entidade: string) => ENTIDADES.find((e) => e.valor === entidade)?.rotulo ?? entidade;

/**
 * Trilha de auditoria da clínica: quem criou, alterou, inativou ou excluiu cada cadastro e
 * registro clínico, e quando. Logins e consultas não aparecem: a tela se concentra no que
 * mudou nos dados.
 */
export function Auditoria() {
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [acao, setAcao] = useState('');
  const [entidade, setEntidade] = useState('');
  const [usuarioId, setUsuarioId] = useState('');
  const [inicio, setInicio] = useState(trintaDiasAtras);
  const [fim, setFim] = useState(paraValorInputData(new Date()));
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);

  // A lista de usuários alimenta o filtro "quem fez"; falhar aqui só esconde o filtro.
  useEffect(() => {
    let ativo = true;

    api
      .get<PaginaDe<Usuario>>('/api/usuarios', { params: { tamanho: 100 } })
      .then(({ data }) => {
        if (ativo) setUsuarios(data.itens);
      })
      .catch(() => undefined);

    return () => {
      ativo = false;
    };
  }, []);

  const buscar = useCallback(async () => {
    const { data } = await api.get<PaginaDe<RegistroAuditoria>>('/api/auditoria', {
      params: {
        acao: acao || undefined,
        entidade: entidade || undefined,
        usuarioId: usuarioId || undefined,
        inicio,
        fim,
        pagina: numeroPagina,
        tamanho: 25,
      },
    });

    return data;
  }, [acao, entidade, usuarioId, inicio, fim, numeroPagina]);

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
        rotuloDaAcao(r.acao),
        rotuloDaEntidade(r.entidade),
        r.detalhe,
        r.enderecoIp,
      ]),
    );
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Trilha de auditoria"
        descricao="Quem criou, alterou, inativou ou excluiu cada cadastro e registro clínico."
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
        <Alerta flutuante tipo="erro" aoFechar={() => setErro('')}>
          {erro}
        </Alerta>
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
                <option key={item.valor} value={item.valor}>
                  {item.rotulo}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="entidade" className="vc-rotulo">
              Registro
            </label>
            <select
              id="entidade"
              className="vc-campo w-auto"
              value={entidade}
              onChange={(e) => filtrar(() => setEntidade(e.target.value))}
            >
              <option value="">Todas</option>
              {ENTIDADES.map((item) => (
                <option key={item.valor} value={item.valor}>
                  {item.rotulo}
                </option>
              ))}
            </select>
          </div>

          {usuarios.length > 0 && (
            <div>
              <label htmlFor="usuario" className="vc-rotulo">
                Usuário
              </label>
              <select
                id="usuario"
                className="vc-campo w-auto max-w-56"
                value={usuarioId}
                onChange={(e) => filtrar(() => setUsuarioId(e.target.value))}
              >
                <option value="">Todos</option>
                {usuarios.map((usuario) => (
                  <option key={usuario.id} value={usuario.id}>
                    {usuario.nome}
                  </option>
                ))}
              </select>
            </div>
          )}

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

          <button type="button" className="vc-botao-secundario" onClick={recarregar} disabled={carregando}>
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
            descricao="Criações, alterações, inativações e exclusões de cadastros e registros clínicos aparecem aqui."
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="vc-tabela">
              <thead>
                <tr>
                  <th scope="col">Data e hora</th>
                  <th scope="col">Usuário</th>
                  <th scope="col">Ação</th>
                  <th scope="col">Registro</th>
                  <th scope="col">Detalhe</th>
                  <th scope="col">Origem</th>
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
                      <Etiqueta
                        className={ACOES.find((a) => a.valor === registro.acao)?.estilo ?? 'bg-slate-100 text-slate-600'}
                      >
                        {rotuloDaAcao(registro.acao)}
                      </Etiqueta>
                    </td>
                    <td className="text-xs text-slate-500">{registro.entidade ? rotuloDaEntidade(registro.entidade) : '—'}</td>
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
