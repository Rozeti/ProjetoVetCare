import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Activity,
  ArrowLeft,
  ClipboardList,
  Download,
  FileText,
  Loader2,
  Lock,
  Paperclip,
  Pencil,
  Plus,
  Printer,
  ShieldAlert,
  Stethoscope,
} from 'lucide-react';
import { api, mensagemDeErro, urlDoArquivo } from '../services/api';
import { useAuth } from '../contexts/auth';
import { useCarregamento } from '../hooks/useCarregamento';
import type { Documento, ItemLinhaTempo, Prontuario as ProntuarioDTO } from '../types';
import {
  Alerta,
  CabecalhoPagina,
  Card,
  Carregando,
  Etiqueta,
  SemDados,
} from '../components/ui';
import { GraficoEvolucao } from '../components/GraficoEvolucao';
import {
  estiloEscalaDor,
  estiloStatusTratamento,
  formatarData,
  formatarDataHora,
  formatarPeso,
  formatarTamanho,
} from '../utils/formato';
import { AlertasClinicos } from '../components/AlertasClinicos';
import { imprimirProntuario } from '../utils/impressao';
import { ModalAvaliacao } from './componentes/ModalAvaliacao';
import { ModalTratamento } from './componentes/ModalTratamento';
import { ModalAlertaClinico } from './componentes/ModalAlertaClinico';
import { CarteiraVacinacao } from './componentes/CarteiraVacinacao';
import { Receituario } from './componentes/Receituario';

type Aba = 'linha-do-tempo' | 'evolucao' | 'vacinas' | 'receitas' | 'tratamentos' | 'documentos';

/** HU-011: prontuário com linha do tempo, mídias e indicadores de evolução. */
export function Prontuario() {
  const { pacienteId = '' } = useParams();
  const { podeVerObservacoesInternas, temPerfil, ehTutor } = useAuth();

  const podeRegistrar = temPerfil('Administrador', 'Veterinario');
  const podeAnexarDocumento = temPerfil('Administrador', 'Veterinario', 'Apoio');

  // O tutor não alcança a lista geral de pacientes: ele volta para os próprios pets.
  const voltarPara = ehTutor ? '/meus-pets' : '/pacientes';
  const rotuloVoltar = ehTutor ? 'Voltar para meus pets' : 'Voltar para pacientes';

  const [aba, setAba] = useState<Aba>('linha-do-tempo');
  const [aviso, setAviso] = useState('');
  const [modalAvaliacao, setModalAvaliacao] = useState(false);
  const [modalTratamento, setModalTratamento] = useState(false);
  const [modalAlerta, setModalAlerta] = useState(false);
  const [enviandoDocumento, setEnviandoDocumento] = useState(false);
  const [nomeClinica, setNomeClinica] = useState('Clínica VetSPA');

  const buscar = useCallback(async () => {
    const { data } = await api.get<ProntuarioDTO>(`/api/prontuarios/paciente/${pacienteId}`);
    return data;
  }, [pacienteId]);

  const {
    dados: prontuario,
    carregando,
    erro,
    setErro,
    recarregar,
  } = useCarregamento(buscar, 'Não foi possível carregar o prontuário.');

  // O nome da clínica compõe o cabeçalho dos documentos impressos.
  useEffect(() => {
    api
      .get<{ nome: string }>('/api/clinica')
      .then(({ data }) => setNomeClinica(data.nome))
      .catch(() => undefined);
  }, []);

  /** HU-012: anexo de contratos, exames externos e laudos. */
  async function enviarDocumento(arquivo: File, tipo: string) {
    setErro('');
    setEnviandoDocumento(true);

    const corpo = new FormData();
    corpo.append('pacienteId', pacienteId);
    corpo.append('tipoDocumento', tipo);
    corpo.append('arquivo', arquivo);

    try {
      await api.post('/api/documentos', corpo);
      setAviso('Documento anexado com sucesso.');
      recarregar();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível anexar o documento.'));
    } finally {
      setEnviandoDocumento(false);
    }
  }

  /** HU-012, CA-2: o download passa pela API, que valida a autorização do usuário. */
  async function baixarDocumento(documento: Documento) {
    try {
      const { data } = await api.get<Blob>(`/api/documentos/${documento.id}/download`, { responseType: 'blob' });

      const url = URL.createObjectURL(data);
      const link = document.createElement('a');
      link.href = url;
      link.download = documento.nomeArquivo;
      link.click();
      URL.revokeObjectURL(url);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível baixar o documento.'));
    }
  }

  if (carregando) {
    return <Carregando texto="Carregando prontuário..." />;
  }

  if (!prontuario) {
    return (
      <>
        <Link to={voltarPara} className="mb-4 inline-flex items-center gap-1 text-sm text-brand hover:underline">
          <ArrowLeft size={15} />
          Voltar
        </Link>
        <Alerta tipo="erro">{erro || 'Prontuário não encontrado.'}</Alerta>
      </>
    );
  }

  const dosesPendentes = prontuario.vacinas.filter(
    (v) => v.situacaoDose === 'Vencida' || v.situacaoDose === 'A vencer',
  ).length;

  const abas: { valor: Aba; rotulo: string; contador?: number; alerta?: boolean }[] = [
    { valor: 'linha-do-tempo', rotulo: 'Linha do tempo', contador: prontuario.historico.length },
    { valor: 'evolucao', rotulo: 'Evolução' },
    { valor: 'vacinas', rotulo: 'Vacinação', contador: dosesPendentes, alerta: dosesPendentes > 0 },
    { valor: 'receitas', rotulo: 'Receitas', contador: prontuario.prescricoes.length },
    { valor: 'tratamentos', rotulo: 'Tratamentos', contador: prontuario.tratamentos.length },
    { valor: 'documentos', rotulo: 'Documentos', contador: prontuario.documentos.length },
  ];

  return (
    <>
      <Link to={voltarPara} className="mb-4 inline-flex items-center gap-1 text-sm text-brand hover:underline">
        <ArrowLeft size={15} />
        {rotuloVoltar}
      </Link>

      <CabecalhoPagina
        titulo={prontuario.nomePaciente}
        descricao={[
          prontuario.especie,
          prontuario.raca,
          prontuario.idadeDescritiva,
          prontuario.sexo,
          prontuario.castrado ? 'Castrado' : null,
          prontuario.microchip ? `Chip ${prontuario.microchip}` : null,
          `Tutor: ${prontuario.nomeTutor}`,
        ]
          .filter(Boolean)
          .join(' · ')}
        acoes={
          <>
            <button
              type="button"
              className="vc-botao-secundario"
              onClick={() => imprimirProntuario(prontuario, nomeClinica)}
            >
              <Printer size={16} />
              Imprimir
            </button>

            {podeRegistrar && (
              <>
                <button type="button" className="vc-botao-secundario" onClick={() => setModalAlerta(true)}>
                  <ShieldAlert size={16} />
                  Alerta clínico
                </button>
                <button type="button" className="vc-botao-secundario" onClick={() => setModalTratamento(true)}>
                  <Plus size={16} />
                  Tratamento
                </button>
                <button type="button" className="vc-botao-primario" onClick={() => setModalAvaliacao(true)}>
                  <ClipboardList size={16} />
                  Registrar avaliação
                </button>
              </>
            )}
          </>
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

      {prontuario.dataObito && (
        <div className="mb-4">
          <Alerta tipo="aviso">
            Paciente registrado como falecido em {formatarData(prontuario.dataObito)}. O prontuário
            permanece disponível apenas para consulta.
          </Alerta>
        </div>
      )}

      {/* Alergias e comorbidades precisam ser lidas antes de qualquer conduta. */}
      {prontuario.alertasClinicos.length > 0 && (
        <div className="mb-6">
          <AlertasClinicos alertas={prontuario.alertasClinicos} />
        </div>
      )}

      {/*
        HU-011, CA-2 e RN-003: quem não é Administrador nem Veterinário recebe a versão
        filtrada. O texto distingue o Tutor do Apoio administrativo, que também não
        enxerga as anotações internas.
      */}
      {!prontuario.exibeObservacoesInternas && (
        <div className="mb-4">
          <Alerta tipo="info">
            {ehTutor
              ? 'Você está vendo as informações autorizadas do prontuário do seu pet. As anotações internas da equipe clínica não fazem parte desta visão.'
              : 'Seu perfil acessa o prontuário sem as observações internas, que são restritas a Administrador e Veterinário.'}
          </Alerta>
        </div>
      )}

      <div className="mb-6 flex flex-wrap gap-1 border-b border-slate-200" role="tablist">
        {abas.map((item) => (
          <button
            key={item.valor}
            type="button"
            role="tab"
            aria-selected={aba === item.valor}
            onClick={() => setAba(item.valor)}
            className={`-mb-px border-b-2 px-4 py-2.5 text-sm font-medium transition ${
              aba === item.valor
                ? 'border-brand text-brand'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}
          >
            {item.rotulo}
            {item.contador !== undefined && item.contador > 0 && (
              <span
                className={`ml-1.5 rounded-full px-1.5 py-0.5 text-xs ${
                  item.alerta ? 'bg-alerta-claro font-semibold text-amber-800' : 'bg-slate-100 text-slate-600'
                }`}
              >
                {item.contador}
              </span>
            )}
          </button>
        ))}
      </div>

      {aba === 'linha-do-tempo' && (
        <LinhaDoTempo itens={prontuario.historico} exibeObservacoes={podeVerObservacoesInternas} />
      )}

      {aba === 'evolucao' && (
        <div className="grid gap-6 lg:grid-cols-2">
          <Card className="p-5">
            <GraficoEvolucao titulo="Evolução de peso" pontos={prontuario.evolucaoPeso} unidade="kg" cor="#0284c7" />
          </Card>

          <Card className="p-5">
            <GraficoEvolucao
              titulo="Escala de dor"
              pontos={prontuario.evolucaoDor}
              cor="#dc2626"
              minimoFixo={0}
              maximoFixo={10}
            />
          </Card>
        </div>
      )}

      {aba === 'vacinas' && (
        <CarteiraVacinacao pacienteId={pacienteId} vacinas={prontuario.vacinas} aoAtualizar={recarregar} />
      )}

      {aba === 'receitas' && (
        <Receituario
          pacienteId={pacienteId}
          prescricoes={prontuario.prescricoes}
          nomeClinica={nomeClinica}
          aoAtualizar={recarregar}
        />
      )}

      {aba === 'tratamentos' && (
        <div className="space-y-4">
          {prontuario.tratamentos.length === 0 ? (
            <Card>
              <SemDados
                icone={<Stethoscope size={40} />}
                titulo="Nenhum tratamento registrado"
                descricao="Abra um tratamento para começar a agendar sessões e registrar a evolução."
                acao={
                  podeRegistrar ? (
                    <button type="button" className="vc-botao-sutil" onClick={() => setModalTratamento(true)}>
                      <Plus size={16} />
                      Novo tratamento
                    </button>
                  ) : undefined
                }
              />
            </Card>
          ) : (
            prontuario.tratamentos.map((tratamento) => (
              <Card key={tratamento.id} className="p-5">
                <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold text-slate-900">{tratamento.objetivoTerapeutico}</p>
                    <p className="mt-0.5 text-xs text-slate-500">
                      {tratamento.nomeVeterinario} · Início em {formatarData(tratamento.dataInicio)}
                      {tratamento.dataFim && ` · Encerrado em ${formatarData(tratamento.dataFim)}`}
                    </p>
                  </div>

                  <Etiqueta className={estiloStatusTratamento[tratamento.status] ?? 'bg-slate-200 text-slate-700'}>
                    {tratamento.status}
                  </Etiqueta>
                </div>

                {tratamento.observacoesGerais && (
                  <p className="mb-3 text-sm text-slate-600">{tratamento.observacoesGerais}</p>
                )}

                <div className="flex items-center gap-2 text-xs text-slate-500">
                  <Activity size={14} />
                  {tratamento.sessoesConcluidas} de {tratamento.totalSessoes} sessões concluídas
                </div>

                {tratamento.totalSessoes > 0 && (
                  <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100">
                    <div
                      className="h-full rounded-full bg-brand transition-all"
                      style={{ width: `${(tratamento.sessoesConcluidas / tratamento.totalSessoes) * 100}%` }}
                    />
                  </div>
                )}
              </Card>
            ))
          )}
        </div>
      )}

      {aba === 'documentos' && (
        <Card>
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
            <h2 className="font-semibold text-slate-900">Documentos clínicos</h2>

            {podeAnexarDocumento && (
              <div className="flex items-center gap-2">
                <select id="tipo-documento" className="vc-campo w-auto py-2" defaultValue="Exame">
                  <option>Contrato</option>
                  <option>Exame</option>
                  <option>Laudo</option>
                  <option>Outro</option>
                </select>

                <label className="vc-botao-secundario cursor-pointer">
                  {enviandoDocumento ? <Loader2 className="animate-spin" size={16} /> : <Paperclip size={16} />}
                  Anexar
                  <input
                    type="file"
                    className="sr-only"
                    accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
                    disabled={enviandoDocumento}
                    onChange={(e) => {
                      const arquivo = e.target.files?.[0];
                      const tipo = (document.getElementById('tipo-documento') as HTMLSelectElement | null)?.value ?? 'Outro';

                      if (arquivo) enviarDocumento(arquivo, tipo);
                      e.target.value = '';
                    }}
                  />
                </label>
              </div>
            )}
          </div>

          {prontuario.documentos.length === 0 ? (
            <SemDados
              icone={<FileText size={40} />}
              titulo="Nenhum documento anexado"
              descricao="Contratos, exames externos e laudos em PDF ficam centralizados aqui. Formatos aceitos: PDF, JPG, PNG, DOC e DOCX (até 10 MB)."
            />
          ) : (
            <ul className="divide-y divide-slate-100">
              {prontuario.documentos.map((documento) => (
                <li key={documento.id} className="flex flex-wrap items-center gap-4 px-5 py-4">
                  <div className="rounded-xl bg-brand-50 p-2.5 text-brand">
                    <FileText size={20} />
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium text-slate-900">{documento.nomeArquivo}</p>
                    <p className="text-xs text-slate-500">
                      {documento.tipoDocumento} · {formatarTamanho(documento.tamanhoBytes)} ·{' '}
                      {formatarData(documento.dataUpload)}
                      {documento.enviadoPor && ` · ${documento.enviadoPor}`}
                    </p>
                  </div>

                  <button
                    type="button"
                    onClick={() => baixarDocumento(documento)}
                    className="vc-botao-secundario"
                  >
                    <Download size={15} />
                    Baixar
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}

      <ModalAvaliacao
        aberto={modalAvaliacao}
        tratamentos={prontuario.tratamentos}
        aoFechar={() => setModalAvaliacao(false)}
        aoSalvar={() => {
          setModalAvaliacao(false);
          setAviso('Avaliação clínica registrada com sucesso.');
          recarregar();
        }}
      />

      <ModalAlertaClinico
        aberto={modalAlerta}
        pacienteId={pacienteId}
        alertas={prontuario.alertasClinicos}
        aoFechar={() => setModalAlerta(false)}
        aoSalvar={() => {
          setModalAlerta(false);
          setAviso('Alerta clínico registrado.');
          recarregar();
        }}
      />

      <ModalTratamento
        aberto={modalTratamento}
        pacienteId={pacienteId}
        aoFechar={() => setModalTratamento(false)}
        aoSalvar={() => {
          setModalTratamento(false);
          setAviso('Tratamento aberto com sucesso.');
          recarregar();
        }}
      />
    </>
  );
}

/** HU-011, CA-1: avaliações e atendimentos em ordem cronológica, com mídias na posição correspondente. */
function LinhaDoTempo({ itens, exibeObservacoes }: { itens: ItemLinhaTempo[]; exibeObservacoes: boolean }) {
  const [expandidos, setExpandidos] = useState<Set<string>>(new Set());

  function alternar(id: string) {
    setExpandidos((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  if (itens.length === 0) {
    return (
      <Card>
        <SemDados
          icone={<ClipboardList size={40} />}
          titulo="Prontuário ainda sem registros"
          descricao="Assim que a primeira avaliação ou atendimento for registrado, o histórico aparece aqui em ordem cronológica."
        />
      </Card>
    );
  }

  const icone: Record<string, typeof ClipboardList> = {
    'Avaliação Clínica': ClipboardList,
    Atendimento: Stethoscope,
    'Observação Interna': Lock,
  };

  return (
    <ol className="relative space-y-4 border-l-2 border-slate-200 pl-6">
      {itens.map((item) => {
        const Icone = icone[item.tipo] ?? ClipboardList;
        const aberto = expandidos.has(item.id);

        return (
          <li key={item.id} className="relative">
            <span className="absolute -left-[33px] flex h-6 w-6 items-center justify-center rounded-full border-2 border-white bg-brand text-white">
              <Icone size={12} />
            </span>

            <Card className="p-5">
              <div className="mb-2 flex flex-wrap items-start justify-between gap-2">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="font-semibold text-slate-900">{item.tipo}</h3>

                    {item.escalaDor != null && (
                      <Etiqueta className={estiloEscalaDor(item.escalaDor)}>Dor {item.escalaDor}/10</Etiqueta>
                    )}

                    {item.pesoKg != null && (
                      <Etiqueta className="bg-slate-100 text-slate-600">{formatarPeso(item.pesoKg)}</Etiqueta>
                    )}

                    {/* RN-004: a edição é sinalizada, preservando a rastreabilidade. */}
                    {item.editado && (
                      <Etiqueta className="bg-alerta-claro text-amber-800">
                        <Pencil size={11} />
                        Editado
                      </Etiqueta>
                    )}
                  </div>

                  <p className="mt-0.5 text-xs text-slate-500">
                    {formatarDataHora(item.data)}
                    {item.autor && ` · ${item.autor}`}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => alternar(item.id)}
                  className="text-sm font-medium text-brand hover:underline"
                >
                  {aberto ? 'Recolher' : 'Ver detalhes'}
                </button>
              </div>

              <p className="text-sm font-medium text-slate-800">{item.descricao}</p>
              {item.detalhes && <p className="mt-1 text-sm text-slate-600">{item.detalhes}</p>}

              {aberto && Object.keys(item.campos).length > 0 && (
                <dl className="mt-4 grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-2">
                  {Object.entries(item.campos).map(([rotulo, valor]) => (
                    <div key={rotulo}>
                      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{rotulo}</dt>
                      <dd className="mt-0.5 text-sm text-slate-700">{valor}</dd>
                    </div>
                  ))}
                </dl>
              )}

              {/* HU-011, CA-4: fotos e vídeos exibidos na linha do tempo. */}
              {item.midias.length > 0 && (
                <ul className="mt-4 grid gap-2 sm:grid-cols-4">
                  {item.midias.map((midia) => (
                    <li key={midia.id} className="overflow-hidden rounded-xl border border-slate-200">
                      {midia.tipo === 'Video' ? (
                        <video src={urlDoArquivo(midia.urlArquivo)} controls className="h-24 w-full bg-slate-900 object-cover" />
                      ) : (
                        <a href={urlDoArquivo(midia.urlArquivo)} target="_blank" rel="noreferrer">
                          <img
                            src={urlDoArquivo(midia.urlArquivo)}
                            alt={midia.nomeArquivo}
                            className="h-24 w-full object-cover transition hover:opacity-90"
                            loading="lazy"
                          />
                        </a>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              {/* RN-003: o bloco só existe quando o perfil autoriza; para o tutor a lista chega vazia da API. */}
              {exibeObservacoes && item.observacoesInternas.length > 0 && (
                <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50/70 p-4">
                  <p className="mb-2 flex items-center gap-1.5 text-xs font-bold uppercase tracking-wide text-amber-800">
                    <Lock size={12} />
                    Observação interna — não visível ao tutor
                  </p>

                  {item.observacoesInternas.map((observacao) => (
                    <div key={observacao.id} className="mt-2 first:mt-0">
                      <p className="text-sm text-amber-900">{observacao.conteudo}</p>
                      <p className="mt-0.5 text-xs text-amber-700">
                        {observacao.autor} · {formatarDataHora(observacao.dataRegistro)}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </Card>
          </li>
        );
      })}
    </ol>
  );
}
