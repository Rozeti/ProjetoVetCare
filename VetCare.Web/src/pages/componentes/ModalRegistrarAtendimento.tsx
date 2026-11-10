import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ClipboardPlus, FileText, Loader2, Lock, Paperclip, Pencil, Trash2 } from 'lucide-react';
import { api, mensagemDeErro, urlDoArquivo } from '../../services/api';
import { useConfirmacao } from '../../hooks/useConfirmacao';
import { useAuth } from '../../contexts/auth';
import type { Atendimento, Midia, SessaoParaRegistro, Tratamento } from '../../types';
import { Alerta, Campo, Etiqueta, Modal } from '../../components/ui';
import { estiloEscalaDor, formatarData, formatarDataHora, formatarIntervalo } from '../../utils/formato';
import { ModalReceitaOuExame } from './ModalReceitaOuExame';
import { ABA_DA_EMISSAO, type TipoDeEmissao } from '../../utils/vinculoClinico';

interface Props {
  /** Sessão cadastrada: registra o atendimento dela (ou abre o registro existente, para anexos). */
  sessao?: SessaoParaRegistro | null;
  /** Registro já feito, aberto a partir do prontuário ou da tela de sessões. */
  atendimento?: Atendimento | null;
  /** Abre o registro direto no formulário de correção (RN-004), em vez do resumo. */
  corrigir?: boolean;
  /**
   * Receita ou pedido de exame emitido a partir desta sessão. Sem o callback, o modal leva ao
   * prontuário do animal, na aba do documento emitido.
   */
  aoEmitirDocumento?: (tipo: TipoDeEmissao, mensagem: string) => void;
  /** Falso quando o prontuário está encerrado (óbito) e não aceita documentos novos. */
  permiteEmissao?: boolean;
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

/** Mesmos formatos de AnexarMidiaUseCase (RN-011). */
const FORMATOS_DE_MIDIA = ['.jpg', '.jpeg', '.png', '.webp', '.mp4', '.mov'];

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
 * HU-008: registro do atendimento de uma sessão cadastrada — o número da sessão no tratamento,
 * o que foi feito, a reação do animal, observações, relatório, procedimentos e fotografias. A
 * correção posterior preserva a versão anterior (RN-004). O registro nunca cria sessões: elas
 * são cadastradas antes, no tratamento.
 * HU-009: permite anexar uma observação interna, restrita à equipe clínica.
 * HU-010: anexa fotos e vídeos à sessão.
 */
export function ModalRegistrarAtendimento({ sessao, atendimento, ...props }: Props) {
  // O modal só existe quando há algo a mostrar: cada abertura monta um formulário
  // novo, o que dispensa um efeito para limpá-lo.
  if (!sessao && !atendimento) {
    return null;
  }

  return <Formulario sessao={sessao ?? null} atendimento={atendimento ?? null} {...props} />;
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
  corrigir = false,
  aoEmitirDocumento,
  permiteEmissao = true,
  exibirLinkDoProntuario = false,
  aoFechar,
  aoSalvar,
}: Omit<Props, 'sessao' | 'atendimento'> & {
  sessao: SessaoParaRegistro | null;
  atendimento: Atendimento | null;
}) {
  const { podeVerObservacoesInternas, temPerfil } = useAuth();
  const podeEmitir = permiteEmissao && temPerfil('Administrador', 'Veterinario');
  const navigate = useNavigate();

  const [form, setForm] = useState<Estado>(atendimento ? estadoAPartirDe(atendimento) : ESTADO_INICIAL);
  const [atendimentoSalvo, setAtendimentoSalvo] = useState<Atendimento | null>(atendimento);

  // A sessão sempre existe antes do registro: as fotos e os vídeos vão direto para ela.
  const sessaoId = atendimentoSalvo?.sessaoId ?? sessao?.sessaoId ?? atendimento?.sessaoId ?? '';
  const [editando, setEditando] = useState(!!atendimento && corrigir);
  const [midias, setMidias] = useState<Midia[]>(atendimento?.midias ?? []);
  // Tratamento da sessão: dá o nome e o número que o novo registro vai receber.
  const [tratamento, setTratamento] = useState<Tratamento | null>(null);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [enviandoMidia, setEnviandoMidia] = useState(false);
  const [houveAlteracao, setHouveAlteracao] = useState(false);
  // Receita ou pedido de exame desta sessão: o registro dá lugar ao formulário de emissão.
  const [emitindo, setEmitindo] = useState(false);

  const campoArquivo = useRef<HTMLInputElement>(null);

  // A sessão pode já ter o atendimento registrado: nesse caso o modal abre em modo resumo.
  useEffect(() => {
    if (!sessao) {
      return;
    }

    let ativo = true;

    async function carregarExistente(item: SessaoParaRegistro) {
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
        const { data } = await api.post<Atendimento>('/api/atendimentos', {
          sessaoId,
          ...campos,
          observacaoInterna: form.observacaoInterna.trim() || null,
          concluirSessao: true,
        });

        // As fotos anexadas antes de salvar já estão na sessão e vêm junto com o registro.
        setAtendimentoSalvo(data);
        setMidias(data.midias);
        setHouveAlteracao(true);
        setAviso(`Atendimento registrado: sessão nº ${data.numeroSessao}, concluída.`);
      }
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível salvar a sessão.'));
    } finally {
      setSalvando(false);
    }
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

  /** Depois da emissão, o usuário vai para a aba do documento: no prontuário aberto ou abrindo-o. */
  function concluirEmissao(tipo: TipoDeEmissao, mensagem: string) {
    setEmitindo(false);

    if (aoEmitirDocumento) {
      aoEmitirDocumento(tipo, mensagem);
      return;
    }

    if (atendimentoSalvo) {
      navigate(`/prontuario/${atendimentoSalvo.pacienteId}?aba=${ABA_DA_EMISSAO[tipo]}`, { state: { aviso: mensagem } });
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
    : atendimentoSalvo
      ? atendimentoSalvo.nomePaciente
      : '';
  const mostrarFormulario = !atendimentoSalvo || editando;

  // Número e tratamento do registro: o registrado, ou o que o próximo vai receber.
  const nomeTratamento = atendimentoSalvo?.nomeTratamento || tratamento?.nome || '';
  const numeroSessao = atendimentoSalvo?.numeroSessao ?? (tratamento ? tratamento.evolucoesRegistradas + 1 : null);

  const titulo =
    editando && atendimentoSalvo
      ? 'Corrigir registro da sessão'
      : atendimentoSalvo
        ? `Sessão nº ${atendimentoSalvo.numeroSessao}`
        : 'Registrar atendimento';

  if (emitindo && atendimentoSalvo) {
    return (
      <ModalReceitaOuExame
        sessao={{
          id: atendimentoSalvo.id,
          data: atendimentoSalvo.dataRegistro,
          numeroSessao: atendimentoSalvo.numeroSessao,
          tratamentoId: atendimentoSalvo.tratamentoId,
          nomeTratamento: nomeTratamento,
          nomeVeterinario: atendimentoSalvo.nomeVeterinario,
          pacienteId: atendimentoSalvo.pacienteId,
          nomePaciente: atendimentoSalvo.nomePaciente || sessao?.nomePaciente || '',
        }}
        aoFechar={() => setEmitindo(false)}
        aoSalvar={concluirEmissao}
      />
    );
  }

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
      {(nomeTratamento || numeroSessao || sessao) && (
        <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-1 rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
          {sessao && !atendimentoSalvo && (
            <span>
              Sessão de <strong className="text-slate-800">{formatarData(sessao.dataHora)}</strong>,{' '}
              {formatarIntervalo(sessao.dataHora, sessao.dataHoraFim)}
            </span>
          )}
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
            {podeEmitir && (
              <button
                type="button"
                className="vc-botao-secundario"
                onClick={() => {
                  setAviso('');
                  setEmitindo(true);
                }}
              >
                <ClipboardPlus size={15} />
                Receita ou pedido de exame
              </button>
            )}

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
              {editando && atendimentoSalvo ? 'Salvar correção' : 'Salvar atendimento'}
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
              onChange={(e) => e.target.files?.[0] && enviarMidia(e.target.files[0])}
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

        {midias.length === 0 ? (
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
