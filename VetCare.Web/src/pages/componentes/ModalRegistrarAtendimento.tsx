import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { FileText, Loader2, Lock, Paperclip, Pencil, Pill, Trash2, X } from 'lucide-react';
import { api, mensagemDeErro, urlDoArquivo } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import { useAuth } from '../../contexts/auth';
import type { Atendimento, ItemAgenda, Midia, Sessao, Tratamento } from '../../types';
import { Alerta, Campo, Etiqueta, Modal } from '../../components/ui';
import { estiloEscalaDor, formatarDataHora, paraValorInputData } from '../../utils/formato';

interface Props {
  /** Sessão da agenda: registra uma evolução nova (ou abre a existente para anexos). */
  sessao?: ItemAgenda | null;
  /** Evolução já registrada, aberta a partir do prontuário ou da tela de evoluções. */
  atendimento?: Atendimento | null;
  /**
   * Tratamento: registra a sessão realizada sem passar pela agenda. Quem registra escolhe uma
   * sessão agendada ainda sem evolução ou registra a sessão de agora, sem agendamento.
   */
  tratamento?: Tratamento | null;
  /** Abre a evolução direto no formulário de correção (RN-004), em vez do resumo. */
  corrigir?: boolean;
  /** Descrição exibida no cabeçalho quando não há sessão da agenda em mãos. */
  descricao?: string;
  /**
   * Emissão de receita a partir desta evolução. Sem o callback, o botão leva ao receituário
   * do prontuário do animal com a evolução já escolhida como origem.
   */
  aoEmitirReceita?: (atendimento: Atendimento) => void;
  /** Falso quando o prontuário está encerrado (óbito) e não aceita receitas novas. */
  permiteReceita?: boolean;
  /** Atalho para o prontuário do animal, útil fora dele (tela de evoluções). */
  exibirLinkDoProntuario?: boolean;
  aoFechar: () => void;
  aoSalvar: () => void;
}

const ESTADO_INICIAL = {
  tecnicasAplicadas: '',
  escalaDor: 0,
  evolucaoClinica: '',
  reacaoAnimal: '',
  observacoes: '',
  relatorio: '',
  proximosPassos: '',
  pesoKg: '',
  temperaturaCelsius: '',
  frequenciaCardiaca: '',
  frequenciaRespiratoria: '',
  observacaoInterna: '',
};

type Estado = typeof ESTADO_INICIAL;

/** Mesmos limites de CriarAtendimentoDTO. */
const LIMITES = {
  tecnicasAplicadas: 1000,
  evolucaoClinica: 4000,
  reacaoAnimal: 2000,
  observacoes: 4000,
  relatorio: 4000,
  proximosPassos: 2000,
  observacaoInterna: 2000,
} as const;

/** Mesmos formatos e limite de AnexarMidiaUseCase (RN-011). */
const FORMATOS_DE_MIDIA = ['.jpg', '.jpeg', '.png', '.webp', '.mp4', '.mov'];
const TAMANHO_MAXIMO_MIDIA = 25 * 1024 * 1024;

/** Arquivo escolhido antes de a sessão existir: vai para a API junto com o registro. */
interface MidiaPendente {
  chave: string;
  arquivo: File;
  url: string;
  video: boolean;
}

function extensaoDe(nome: string): string {
  const ponto = nome.lastIndexOf('.');
  return ponto >= 0 ? nome.slice(ponto).toLowerCase() : '';
}

/** Recusa na hora o que a API recusaria no envio. */
function problemaDoArquivo(arquivo: File): string | null {
  if (!FORMATOS_DE_MIDIA.includes(extensaoDe(arquivo.name))) {
    return `"${arquivo.name}" não é um formato aceito. Use JPG, PNG, WEBP, MP4 ou MOV.`;
  }

  if (arquivo.size > TAMANHO_MAXIMO_MIDIA) {
    return `"${arquivo.name}" passa de 25 MB.`;
  }

  return null;
}

/** Faixas clínicas aceitas pela API (CriarAtendimentoDTO), usadas nos campos e na validação. */
const FAIXAS_DE_SINAIS = {
  pesoKg: { min: 0.1, max: 200, passo: 0.1, rotulo: 'o peso', unidade: 'kg' },
  temperaturaCelsius: { min: 30, max: 45, passo: 0.1, rotulo: 'a temperatura', unidade: '°C' },
  frequenciaCardiaca: { min: 10, max: 400, passo: 1, rotulo: 'a frequência cardíaca', unidade: 'bpm' },
  frequenciaRespiratoria: { min: 5, max: 200, passo: 1, rotulo: 'a frequência respiratória', unidade: 'mpm' },
} as const;

/** Devolve a primeira faixa desrespeitada, ou null quando todos os sinais informados são válidos. */
function validarSinaisVitais(form: Pick<Estado, keyof typeof FAIXAS_DE_SINAIS>): string | null {
  for (const campo of Object.keys(FAIXAS_DE_SINAIS) as (keyof typeof FAIXAS_DE_SINAIS)[]) {
    const texto = form[campo].trim();

    if (!texto) continue;

    const valor = Number(texto.replace(',', '.'));
    const faixa = FAIXAS_DE_SINAIS[campo];

    if (!Number.isFinite(valor) || valor < faixa.min || valor > faixa.max) {
      return `Informe ${faixa.rotulo} entre ${faixa.min} e ${faixa.max} ${faixa.unidade}.`;
    }
  }

  return null;
}

/**
 * HU-008: a evolução registra uma sessão realizada dentro de um tratamento — data, número da
 * sessão, o que foi feito, a reação do animal, observações, relatório, procedimentos e
 * fotografias. A correção posterior preserva a versão anterior (RN-004).
 * HU-009: permite anexar uma observação interna, restrita à equipe clínica.
 * HU-010: anexa fotos e vídeos à sessão.
 */
export function ModalRegistrarAtendimento({ sessao, atendimento, tratamento, ...props }: Props) {
  // O modal só existe quando há algo a mostrar: cada abertura monta um formulário
  // novo, o que dispensa um efeito para limpá-lo.
  if (!sessao && !atendimento && !tratamento) {
    return null;
  }

  return (
    <Formulario
      sessao={sessao ?? null}
      atendimento={atendimento ?? null}
      tratamentoDoProntuario={tratamento ?? null}
      {...props}
    />
  );
}

/** Sessões agendadas até hoje, ainda sem evolução: as que podem ter sido realizadas. */
function sessoesARegistrar(sessoes: Sessao[]): Sessao[] {
  const hoje = paraValorInputData(new Date());

  return sessoes
    .filter(
      (s) =>
        s.status !== 'Cancelada' &&
        s.status !== 'Concluída' &&
        !s.possuiAtendimento &&
        paraValorInputData(s.dataHora) <= hoje,
    )
    .sort((a, b) => b.dataHora.localeCompare(a.dataHora));
}

function estadoAPartirDe(atendimento: Atendimento): Estado {
  return {
    tecnicasAplicadas: atendimento.tecnicasAplicadas,
    escalaDor: atendimento.escalaDor,
    evolucaoClinica: atendimento.evolucaoClinica,
    reacaoAnimal: atendimento.reacaoAnimal,
    observacoes: atendimento.observacoes,
    relatorio: atendimento.relatorio,
    proximosPassos: atendimento.proximosPassos,
    pesoKg: atendimento.pesoKg?.toString() ?? '',
    temperaturaCelsius: atendimento.temperaturaCelsius?.toString() ?? '',
    frequenciaCardiaca: atendimento.frequenciaCardiaca?.toString() ?? '',
    frequenciaRespiratoria: atendimento.frequenciaRespiratoria?.toString() ?? '',
    observacaoInterna: '',
  };
}

function Formulario({
  sessao,
  atendimento,
  tratamentoDoProntuario,
  corrigir = false,
  descricao,
  aoEmitirReceita,
  permiteReceita = true,
  exibirLinkDoProntuario = false,
  aoFechar,
  aoSalvar,
}: Omit<Props, 'sessao' | 'atendimento' | 'tratamento'> & {
  sessao: ItemAgenda | null;
  atendimento: Atendimento | null;
  tratamentoDoProntuario: Tratamento | null;
}) {
  const { podeVerObservacoesInternas, temPerfil } = useAuth();
  const podeEmitirReceita = permiteReceita && temPerfil('Administrador', 'Veterinario');

  const [form, setForm] = useState<Estado>(atendimento ? estadoAPartirDe(atendimento) : ESTADO_INICIAL);
  const [atendimentoSalvo, setAtendimentoSalvo] = useState<Atendimento | null>(atendimento);

  // Registrada a partir do tratamento, a sessão só existe depois de salva a evolução.
  const sessaoId = atendimentoSalvo?.sessaoId ?? sessao?.sessaoId ?? atendimento?.sessaoId ?? '';
  const [sessoesPendentes, setSessoesPendentes] = useState<Sessao[] | null>(null);
  // Vazio: sessão de agora, sem agendamento.
  const [sessaoEscolhida, setSessaoEscolhida] = useState('');
  const [editando, setEditando] = useState(!!atendimento && corrigir);
  const [midias, setMidias] = useState<Midia[]>(atendimento?.midias ?? []);
  // Tratamento da sessão: dá o nome e o número que a nova evolução vai receber.
  const [tratamento, setTratamento] = useState<Tratamento | null>(tratamentoDoProntuario);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [enviandoMidia, setEnviandoMidia] = useState(false);
  const [houveAlteracao, setHouveAlteracao] = useState(false);
  // Fotos e vídeos escolhidos antes de salvar, quando a sessão ainda não existe.
  const [midiasPendentes, setMidiasPendentes] = useState<MidiaPendente[]>([]);

  const campoArquivo = useRef<HTMLInputElement>(null);
  const urlsCriadas = useRef<string[]>([]);

  // As pré-visualizações locais são liberadas quando o modal fecha.
  useEffect(() => {
    const urls = urlsCriadas.current;
    return () => urls.forEach((url) => URL.revokeObjectURL(url));
  }, []);

  // Aberto pela agenda, a sessão pode já ter evolução: nesse caso o modal abre em modo resumo.
  useEffect(() => {
    if (!sessao) {
      return;
    }

    let ativo = true;

    async function carregarExistente(item: ItemAgenda) {
      try {
        const [{ data }, { data: doTratamento }] = await Promise.all([
          api.get<Atendimento | null>(`/api/atendimentos/sessao/${item.sessaoId}`),
          api.get<Tratamento>(`/api/tratamentos/${item.tratamentoId}`),
        ]);

        if (!ativo) return;

        setTratamento(doTratamento);

        if (data) {
          setAtendimentoSalvo(data);
          setMidias(data.midias);
        } else {
          const { data: lista } = await api.get<Midia[]>(`/api/midias/sessao/${item.sessaoId}`);
          if (ativo) setMidias(lista);
        }
      } catch (falha) {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar os dados da sessão.'));
      }
    }

    carregarExistente(sessao);

    return () => {
      ativo = false;
    };
  }, [sessao]);

  // Aberto pelo tratamento: as sessões agendadas e ainda sem evolução podem ser escolhidas.
  useEffect(() => {
    if (!tratamentoDoProntuario) {
      return;
    }

    let ativo = true;

    api
      .get<Sessao[]>(`/api/sessoes/tratamento/${tratamentoDoProntuario.id}`)
      .then(({ data }) => {
        if (!ativo) return;

        const pendentes = sessoesARegistrar(data);
        setSessoesPendentes(pendentes);
        setSessaoEscolhida(pendentes[0]?.id ?? '');
      })
      .catch(() => {
        // Sem a lista, ainda dá para registrar a sessão de agora.
        if (ativo) setSessoesPendentes([]);
      });

    return () => {
      ativo = false;
    };
  }, [tratamentoDoProntuario]);

  function atualizar<T extends keyof Estado>(campo: T, valor: Estado[T]) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
  }

  const { confirmarEdicao, confirmarExclusao } = useConfirmacao();

  function validar(): string | null {
    const pendentes = [
      !form.tecnicasAplicadas.trim() && 'os procedimentos realizados',
      !form.evolucaoClinica.trim() && 'o que foi realizado',
      !form.reacaoAnimal.trim() && 'a reação do animal',
    ].filter(Boolean);

    if (pendentes.length > 0) {
      return `Campos obrigatórios pendentes: ${pendentes.join(', ')}.`;
    }

    for (const campo of Object.keys(LIMITES) as (keyof typeof LIMITES)[]) {
      if (form[campo].length > LIMITES[campo]) {
        return `Um dos campos passou do limite de ${LIMITES[campo]} caracteres. Resuma o texto antes de salvar.`;
      }
    }

    // Os sinais vitais entram no prontuário: um valor fora da faixa clínica é recusado
    // aqui, com a mesma faixa que a API aplica (CriarAtendimentoDTO).
    return validarSinaisVitais(form);
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();

    setErro('');

    const problema = validar();

    if (problema) {
      setErro(problema);
      return;
    }

    if (
      atendimentoSalvo &&
      editando &&
      !(await confirmarEdicao(
        'Salvar a correção desta sessão? A versão anterior fica preservada no histórico do prontuário.',
        'Salvar correção',
      ))
    ) {
      return;
    }

    setSalvando(true);

    const campos = {
      tecnicasAplicadas: form.tecnicasAplicadas.trim(),
      escalaDor: form.escalaDor,
      evolucaoClinica: form.evolucaoClinica.trim(),
      reacaoAnimal: form.reacaoAnimal.trim(),
      observacoes: form.observacoes.trim(),
      relatorio: form.relatorio.trim(),
      proximosPassos: form.proximosPassos.trim(),
      // Os sinais vitais estruturados também viram um resumo em texto para a linha do tempo.
      sinaisVitais: montarResumoSinaisVitais(form),
      pesoKg: form.pesoKg ? Number(form.pesoKg) : null,
      temperaturaCelsius: form.temperaturaCelsius ? Number(form.temperaturaCelsius) : null,
      frequenciaCardiaca: form.frequenciaCardiaca ? Number(form.frequenciaCardiaca) : null,
      frequenciaRespiratoria: form.frequenciaRespiratoria ? Number(form.frequenciaRespiratoria) : null,
    };

    try {
      if (atendimentoSalvo && editando) {
        // RN-004: a API arquiva a versão anterior antes de aplicar a correção.
        const { data } = await api.put<Atendimento>(`/api/atendimentos/${atendimentoSalvo.id}`, campos);

        setAtendimentoSalvo({ ...data, midias });
        setEditando(false);
        setHouveAlteracao(true);
        setAviso('Registro da sessão corrigido. A versão anterior foi preservada no histórico.');
      } else {
        // Pela agenda, a sessão já vem escolhida; pelo tratamento, é a sessão agendada
        // escolhida ou, sem ela, a sessão de agora, que a API cria já concluída.
        const origem = sessaoId
          ? { sessaoId }
          : sessaoEscolhida
            ? { sessaoId: sessaoEscolhida }
            : { tratamentoId: tratamentoDoProntuario?.id };

        const { data } = await api.post<Atendimento>('/api/atendimentos', {
          ...origem,
          ...campos,
          observacaoInterna: form.observacaoInterna.trim() || null,
          concluirSessao: true,
        });

        setAtendimentoSalvo(data);
        setHouveAlteracao(true);

        // Os arquivos escolhidos antes de salvar seguem agora para a sessão registrada.
        const { enviadas, falharam } = await enviarPendentes(data);
        setMidias([...enviadas, ...data.midias]);

        setAviso(
          enviadas.length > 0
            ? `Sessão nº ${data.numeroSessao} registrada e concluída, com ${enviadas.length} ${enviadas.length === 1 ? 'arquivo anexado' : 'arquivos anexados'}.`
            : `Sessão nº ${data.numeroSessao} registrada e concluída. Você ainda pode anexar fotos e vídeos desta sessão.`,
        );

        if (falharam.length > 0) {
          setErro(`A sessão foi salva, mas não foi possível anexar: ${falharam.join(', ')}. Tente anexar de novo.`);
        }
      }
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível salvar a sessão.'));
    } finally {
      setSalvando(false);
    }
  }

  /** Envia, depois do registro, os arquivos escolhidos enquanto a sessão ainda não existia. */
  async function enviarPendentes(registro: Atendimento): Promise<{ enviadas: Midia[]; falharam: string[] }> {
    const enviadas: Midia[] = [];
    const falharam: string[] = [];

    for (const pendente of midiasPendentes) {
      const corpo = new FormData();
      corpo.append('sessaoId', registro.sessaoId);
      corpo.append('atendimentoId', registro.id);
      corpo.append('arquivo', pendente.arquivo);

      try {
        const { data } = await api.post<Midia>('/api/midias', corpo);
        enviadas.unshift(data);
      } catch {
        falharam.push(pendente.arquivo.name);
      }
    }

    setMidiasPendentes([]);
    return { enviadas, falharam };
  }

  /** Com a sessão registrada, o arquivo vai direto; antes disso, espera o salvamento. */
  function escolherArquivo(arquivo: File) {
    if (campoArquivo.current) campoArquivo.current.value = '';

    if (sessaoId) {
      enviarMidia(arquivo);
      return;
    }

    const problema = problemaDoArquivo(arquivo);

    if (problema) {
      setErro(problema);
      return;
    }

    const url = URL.createObjectURL(arquivo);
    urlsCriadas.current.push(url);

    setMidiasPendentes((atual) => [
      ...atual,
      {
        chave: `${arquivo.name}-${arquivo.lastModified}-${atual.length}`,
        arquivo,
        url,
        video: ['.mp4', '.mov'].includes(extensaoDe(arquivo.name)),
      },
    ]);
  }

  function retirarPendente(chave: string) {
    setMidiasPendentes((atual) => atual.filter((p) => p.chave !== chave));
  }

  /** HU-010: upload de mídia vinculada à sessão. */
  async function enviarMidia(arquivo: File) {
    setErro('');
    setEnviandoMidia(true);

    const corpo = new FormData();
    corpo.append('sessaoId', sessaoId);
    corpo.append('arquivo', arquivo);

    if (atendimentoSalvo) {
      corpo.append('atendimentoId', atendimentoSalvo.id);
    }

    try {
      const { data } = await api.post<Midia>('/api/midias', corpo);
      setMidias((atual) => [data, ...atual]);
      setHouveAlteracao(true);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível anexar o arquivo.'));
    } finally {
      setEnviandoMidia(false);
      if (campoArquivo.current) campoArquivo.current.value = '';
    }
  }

  async function removerMidia(id: string) {
    if (!(await confirmarExclusao('Remover este arquivo da sessão? Ele deixa de aparecer no prontuário.', 'Remover'))) {
      return;
    }

    try {
      await api.delete(`/api/midias/${id}`);
      setMidias((atual) => atual.filter((m) => m.id !== id));
      setHouveAlteracao(true);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível remover o arquivo.'));
    }
  }

  const cabecalho = sessao
    ? `${sessao.nomePaciente} · ${sessao.nomeTutor}`
    : descricao ?? (atendimentoSalvo ? atendimentoSalvo.nomePaciente : '');
  const mostrarFormulario = !atendimentoSalvo || editando;

  // Número e tratamento da evolução: o registrado, ou o que a próxima vai receber.
  const nomeTratamento = atendimentoSalvo?.nomeTratamento || tratamento?.nome || '';
  const numeroSessao = atendimentoSalvo?.numeroSessao ?? (tratamento ? tratamento.evolucoesRegistradas + 1 : null);

  const titulo =
    editando && atendimentoSalvo
      ? 'Corrigir registro da sessão'
      : atendimentoSalvo
        ? `Sessão nº ${atendimentoSalvo.numeroSessao}`
        : 'Registrar sessão realizada';

  return (
    <Modal
      aberto
      titulo={titulo}
      descricao={cabecalho}
      aoFechar={() => {
        // Se algo foi registrado ou alterado, a tela de origem precisa se atualizar ao fechar.
        if (houveAlteracao) aoSalvar();
        else aoFechar();
      }}
      largura="max-w-3xl"
    >
      {(nomeTratamento || numeroSessao) && (
        <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-1 rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
          {nomeTratamento && (
            <span>
              Tratamento: <strong className="text-slate-800">{nomeTratamento}</strong>
            </span>
          )}
          {numeroSessao && (
            <span>
              Sessão nº <strong className="text-slate-800">{numeroSessao}</strong>
              {tratamento && ` de ${tratamento.sessoesPrevistas} previstas`}
            </span>
          )}
          {atendimentoSalvo && (
            <span>
              {formatarDataHora(atendimentoSalvo.dataRegistro)}
              {atendimentoSalvo.nomeVeterinario && ` · ${atendimentoSalvo.nomeVeterinario}`}
            </span>
          )}
        </div>
      )}

      {aviso && (
        <Alerta flutuante tipo="sucesso" aoFechar={() => setAviso('')}>
          {aviso}
        </Alerta>
      )}

      {!mostrarFormulario && atendimentoSalvo ? (
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <ResumoCampo rotulo="Procedimentos realizados" valor={atendimentoSalvo.tecnicasAplicadas} />
            <div>
              <p className="vc-rotulo">Escala de dor</p>
              <Etiqueta className={estiloEscalaDor(atendimentoSalvo.escalaDor)}>
                {atendimentoSalvo.escalaDor}/10
              </Etiqueta>
            </div>
            <ResumoCampo rotulo="O que foi realizado" valor={atendimentoSalvo.evolucaoClinica} />
            <ResumoCampo rotulo="Reação do animal" valor={atendimentoSalvo.reacaoAnimal} />
            <ResumoCampo rotulo="Observações" valor={atendimentoSalvo.observacoes} />
            <ResumoCampo rotulo="Relatório" valor={atendimentoSalvo.relatorio} />
            <ResumoCampo rotulo="Próximos passos" valor={atendimentoSalvo.proximosPassos} />
            <ResumoCampo
              rotulo="Sinais vitais"
              valor={atendimentoSalvo.sinaisVitais || resumoDosSinaisRegistrados(atendimentoSalvo)}
            />
          </div>

          <div className="flex flex-wrap justify-end gap-2">
            {podeEmitirReceita &&
              (aoEmitirReceita ? (
                <button type="button" className="vc-botao-secundario" onClick={() => aoEmitirReceita(atendimentoSalvo)}>
                  <Pill size={15} />
                  Emitir receita desta sessão
                </button>
              ) : (
                <Link
                  to={`/prontuario/${atendimentoSalvo.pacienteId}?aba=medicacao&evolucao=${atendimentoSalvo.id}`}
                  className="vc-botao-secundario"
                >
                  <Pill size={15} />
                  Emitir receita desta sessão
                </Link>
              ))}

            {exibirLinkDoProntuario && (
              <Link to={`/prontuario/${atendimentoSalvo.pacienteId}`} className="vc-botao-secundario">
                <FileText size={15} />
                Abrir prontuário
              </Link>
            )}

            <button
              type="button"
              className="vc-botao-secundario"
              onClick={() => {
                setForm(estadoAPartirDe(atendimentoSalvo));
                setAviso('');
                setEditando(true);
              }}
            >
              <Pencil size={15} />
              Corrigir registro
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={aoEnviar} className="space-y-4" noValidate>
          {tratamentoDoProntuario && !atendimentoSalvo && (
            <Campo
              rotulo="Sessão"
              obrigatorio
              dica={
                sessoesPendentes === null
                  ? 'Buscando as sessões agendadas deste tratamento...'
                  : sessoesPendentes.length > 0
                    ? 'Sessões agendadas até hoje e ainda não registradas. Se a sessão não estava agendada, escolha a de agora.'
                    : 'Nenhuma sessão agendada pendente: a sessão de agora entra na agenda já concluída.'
              }
            >
              <select
                className="vc-campo"
                value={sessaoEscolhida}
                onChange={(e) => setSessaoEscolhida(e.target.value)}
                disabled={sessoesPendentes === null}
              >
                {sessoesPendentes?.map((pendente) => (
                  <option key={pendente.id} value={pendente.id}>
                    Agendada para {formatarDataHora(pendente.dataHora)} · {pendente.nomeVeterinario}
                  </option>
                ))}
                <option value="">Sessão de agora, sem agendamento</option>
              </select>
            </Campo>
          )}

          <Campo rotulo="Procedimentos realizados" obrigatorio dica="Separe os procedimentos por vírgula.">
            <input
              className="vc-campo"
              value={form.tecnicasAplicadas}
              onChange={(e) => atualizar('tecnicasAplicadas', e.target.value)}
              placeholder="Hidroterapia, Laserterapia, Cinesioterapia"
              maxLength={LIMITES.tecnicasAplicadas}
            />
          </Campo>

          <Campo rotulo="O que foi realizado" obrigatorio dica={`${form.evolucaoClinica.length}/${LIMITES.evolucaoClinica}`}>
            <textarea
              className="vc-campo"
              rows={3}
              value={form.evolucaoClinica}
              onChange={(e) => atualizar('evolucaoClinica', e.target.value)}
              placeholder="Descreva a sessão: exercícios, tempo de cada técnica, intensidade."
              maxLength={LIMITES.evolucaoClinica}
            />
          </Campo>

          <Campo rotulo="Reação do animal" obrigatorio dica={`${form.reacaoAnimal.length}/${LIMITES.reacaoAnimal}`}>
            <textarea
              className="vc-campo"
              rows={2}
              value={form.reacaoAnimal}
              onChange={(e) => atualizar('reacaoAnimal', e.target.value)}
              placeholder="Como o animal respondeu: tolerância, dor, cansaço, comportamento."
              maxLength={LIMITES.reacaoAnimal}
            />
          </Campo>

          {/* HU-008, CA-2: escala de dor sempre entre 0 e 10. */}
          <Campo rotulo="Escala de dor (0 a 10)" obrigatorio>
            <div className="flex items-center gap-4">
              <input
                type="range"
                min={0}
                max={10}
                step={1}
                className="flex-1 accent-brand"
                value={form.escalaDor}
                onChange={(e) => atualizar('escalaDor', Number(e.target.value))}
                aria-label="Escala de dor"
              />
              <Etiqueta className={estiloEscalaDor(form.escalaDor)}>{form.escalaDor}/10</Etiqueta>
            </div>
          </Campo>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Observações">
              <textarea
                className="vc-campo"
                rows={3}
                value={form.observacoes}
                onChange={(e) => atualizar('observacoes', e.target.value)}
                maxLength={LIMITES.observacoes}
              />
            </Campo>

            <Campo rotulo="Relatório">
              <textarea
                className="vc-campo"
                rows={3}
                value={form.relatorio}
                onChange={(e) => atualizar('relatorio', e.target.value)}
                placeholder="Síntese da sessão para o prontuário e para o tutor."
                maxLength={LIMITES.relatorio}
              />
            </Campo>
          </div>

          <Campo rotulo="Próximos passos">
            <textarea
              className="vc-campo"
              rows={2}
              value={form.proximosPassos}
              onChange={(e) => atualizar('proximosPassos', e.target.value)}
              maxLength={LIMITES.proximosPassos}
            />
          </Campo>

          {/* O peso registrado aqui passa a ser o peso atual do paciente. */}
          <fieldset className="rounded-xl border border-slate-200 p-4">
            <legend className="px-2 text-sm font-semibold text-slate-700">Sinais vitais</legend>

            <div className="grid gap-3 sm:grid-cols-4">
              <Campo rotulo="Peso (kg)" dica={`${FAIXAS_DE_SINAIS.pesoKg.min} a ${FAIXAS_DE_SINAIS.pesoKg.max}`}>
                <input
                  type="number"
                  step={FAIXAS_DE_SINAIS.pesoKg.passo}
                  min={FAIXAS_DE_SINAIS.pesoKg.min}
                  max={FAIXAS_DE_SINAIS.pesoKg.max}
                  className="vc-campo"
                  value={form.pesoKg}
                  onChange={(e) => atualizar('pesoKg', e.target.value)}
                />
              </Campo>

              <Campo
                rotulo="Temp. (°C)"
                dica={`${FAIXAS_DE_SINAIS.temperaturaCelsius.min} a ${FAIXAS_DE_SINAIS.temperaturaCelsius.max}`}
              >
                <input
                  type="number"
                  step={FAIXAS_DE_SINAIS.temperaturaCelsius.passo}
                  min={FAIXAS_DE_SINAIS.temperaturaCelsius.min}
                  max={FAIXAS_DE_SINAIS.temperaturaCelsius.max}
                  className="vc-campo"
                  value={form.temperaturaCelsius}
                  onChange={(e) => atualizar('temperaturaCelsius', e.target.value)}
                />
              </Campo>

              <Campo
                rotulo="FC (bpm)"
                dica={`${FAIXAS_DE_SINAIS.frequenciaCardiaca.min} a ${FAIXAS_DE_SINAIS.frequenciaCardiaca.max}`}
              >
                <input
                  type="number"
                  step={FAIXAS_DE_SINAIS.frequenciaCardiaca.passo}
                  min={FAIXAS_DE_SINAIS.frequenciaCardiaca.min}
                  max={FAIXAS_DE_SINAIS.frequenciaCardiaca.max}
                  className="vc-campo"
                  value={form.frequenciaCardiaca}
                  onChange={(e) => atualizar('frequenciaCardiaca', e.target.value)}
                />
              </Campo>

              <Campo
                rotulo="FR (mpm)"
                dica={`${FAIXAS_DE_SINAIS.frequenciaRespiratoria.min} a ${FAIXAS_DE_SINAIS.frequenciaRespiratoria.max}`}
              >
                <input
                  type="number"
                  step={FAIXAS_DE_SINAIS.frequenciaRespiratoria.passo}
                  min={FAIXAS_DE_SINAIS.frequenciaRespiratoria.min}
                  max={FAIXAS_DE_SINAIS.frequenciaRespiratoria.max}
                  className="vc-campo"
                  value={form.frequenciaRespiratoria}
                  onChange={(e) => atualizar('frequenciaRespiratoria', e.target.value)}
                />
              </Campo>
            </div>
          </fieldset>

          {/* HU-009 / RN-003: observação restrita, nunca visível ao tutor; só no registro inicial. */}
          {!editando && podeVerObservacoesInternas && (
            <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
              <label
                className="mb-2 flex items-center gap-2 text-sm font-semibold text-amber-800"
                htmlFor="evolucao-observacao-interna"
              >
                <Lock size={15} />
                Observação interna
              </label>
              <textarea
                id="evolucao-observacao-interna"
                className="vc-campo"
                rows={2}
                value={form.observacaoInterna}
                onChange={(e) => atualizar('observacaoInterna', e.target.value)}
                placeholder="Anotação restrita à equipe clínica."
                maxLength={LIMITES.observacaoInterna}
              />
              <p className="mt-1.5 text-xs text-amber-700">
                Visível apenas para Administrador e Veterinário. O tutor nunca terá acesso a este conteúdo.
              </p>
            </div>
          )}

          {erro && <Alerta tipo="erro">{erro}</Alerta>}

          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              className="vc-botao-secundario"
              onClick={() => {
                if (editando && atendimentoSalvo && !corrigir) setEditando(false);
                else if (houveAlteracao) aoSalvar();
                else aoFechar();
              }}
              disabled={salvando}
            >
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              {editando && atendimentoSalvo ? 'Salvar correção' : 'Salvar sessão'}
            </button>
          </div>
        </form>
      )}

      {/* HU-010: fotografias e vídeos da sessão. */}
      <div className="mt-6 border-t border-slate-200 pt-5">
        <div className="mb-3 flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-slate-700">Fotografias e vídeos da sessão</h3>

          <label className={`vc-botao-secundario cursor-pointer ${enviandoMidia || salvando ? 'pointer-events-none opacity-60' : ''}`}>
            {enviandoMidia ? <Loader2 className="animate-spin" size={16} /> : <Paperclip size={16} />}
            Anexar
            <input
              ref={campoArquivo}
              type="file"
              className="sr-only"
              accept={FORMATOS_DE_MIDIA.join(',')}
              disabled={enviandoMidia || salvando}
              onChange={(e) => e.target.files?.[0] && escolherArquivo(e.target.files[0])}
            />
          </label>
        </div>

        {erro && !mostrarFormulario && (
          <div className="mb-3">
            <Alerta tipo="erro" aoFechar={() => setErro('')}>
              {erro}
            </Alerta>
          </div>
        )}

        {/* Antes de a sessão existir, os arquivos ficam aqui e seguem junto com o registro. */}
        {midiasPendentes.length > 0 && (
          <ul className="mb-3 grid gap-3 sm:grid-cols-3">
            {midiasPendentes.map((pendente) => (
              <li key={pendente.chave} className="relative overflow-hidden rounded-xl border border-dashed border-brand-300">
                {pendente.video ? (
                  <video src={pendente.url} controls className="h-32 w-full bg-slate-900 object-cover" />
                ) : (
                  <img src={pendente.url} alt={pendente.arquivo.name} className="h-32 w-full object-cover" />
                )}

                <div className="flex items-center justify-between gap-2 px-2 py-1.5">
                  <span className="min-w-0">
                    <span className="block truncate text-xs text-slate-600" title={pendente.arquivo.name}>
                      {pendente.arquivo.name}
                    </span>
                    <span className="block text-[11px] font-medium text-brand">Enviado ao salvar a sessão</span>
                  </span>
                  <button
                    type="button"
                    onClick={() => retirarPendente(pendente.chave)}
                    className="shrink-0 rounded p-1 text-slate-400 hover:bg-perigo-claro hover:text-perigo"
                    aria-label={`Retirar ${pendente.arquivo.name}`}
                  >
                    <X size={14} />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {!sessaoId ? (
          midiasPendentes.length === 0 && (
            <p className="rounded-xl bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
              Anexe fotos e vídeos da sessão: eles são enviados junto com o registro, ao salvar. Formatos aceitos:
              JPG, PNG, WEBP, MP4 e MOV (até 25 MB).
            </p>
          )
        ) : midias.length === 0 ? (
          <p className="rounded-xl bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
            Nenhum arquivo anexado a esta sessão. Formatos aceitos: JPG, PNG, WEBP, MP4 e MOV (até 25 MB).
          </p>
        ) : (
          <ul className="grid gap-3 sm:grid-cols-3">
            {midias.map((midia) => (
              <li key={midia.id} className="group relative overflow-hidden rounded-xl border border-slate-200">
                {midia.tipo === 'Video' ? (
                  <video src={urlDoArquivo(midia.urlArquivo)} controls className="h-32 w-full bg-slate-900 object-cover" />
                ) : (
                  <img
                    src={urlDoArquivo(midia.urlArquivo)}
                    alt={midia.nomeArquivo}
                    className="h-32 w-full object-cover"
                    loading="lazy"
                  />
                )}

                <div className="flex items-center justify-between gap-2 px-2 py-1.5">
                  <span className="truncate text-xs text-slate-600" title={midia.nomeArquivo}>
                    {midia.nomeArquivo}
                  </span>
                  <button
                    type="button"
                    onClick={() => removerMidia(midia.id)}
                    className="shrink-0 rounded p-1 text-slate-400 hover:bg-perigo-claro hover:text-perigo"
                    aria-label={`Remover ${midia.nomeArquivo}`}
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}

function ResumoCampo({ rotulo, valor }: { rotulo: string; valor: string }) {
  if (!valor) return null;

  return (
    <div>
      <p className="vc-rotulo">{rotulo}</p>
      <p className="whitespace-pre-line text-sm text-slate-700">{valor}</p>
    </div>
  );
}

/** Sinais vitais estruturados de uma evolução gravada sem o resumo em texto. */
function resumoDosSinaisRegistrados(atendimento: Atendimento): string {
  const numero = (valor: number) => valor.toString().replace('.', ',');
  const partes: string[] = [];

  if (atendimento.frequenciaCardiaca != null) partes.push(`FC ${atendimento.frequenciaCardiaca} bpm`);
  if (atendimento.frequenciaRespiratoria != null) partes.push(`FR ${atendimento.frequenciaRespiratoria} mpm`);
  if (atendimento.temperaturaCelsius != null) partes.push(`Temp. ${numero(atendimento.temperaturaCelsius)} °C`);
  if (atendimento.pesoKg != null) partes.push(`Peso ${numero(atendimento.pesoKg)} kg`);

  return partes.join(', ');
}

function montarResumoSinaisVitais(form: Estado): string {
  const partes: string[] = [];

  if (form.frequenciaCardiaca) partes.push(`FC ${form.frequenciaCardiaca} bpm`);
  if (form.frequenciaRespiratoria) partes.push(`FR ${form.frequenciaRespiratoria} mpm`);
  if (form.temperaturaCelsius) partes.push(`Temp. ${form.temperaturaCelsius} °C`);
  if (form.pesoKg) partes.push(`Peso ${form.pesoKg} kg`);

  return partes.join(', ');
}
