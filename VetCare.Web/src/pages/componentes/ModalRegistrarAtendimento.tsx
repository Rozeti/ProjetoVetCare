import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Loader2, Lock, Paperclip, Trash2 } from 'lucide-react';
import { api, mensagemDeErro, urlDoArquivo } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { Atendimento, ItemAgenda, Midia } from '../../types';
import { Alerta, Campo, Etiqueta, Modal } from '../../components/ui';
import { estiloEscalaDor } from '../../utils/formato';

interface Props {
  sessao: ItemAgenda | null;
  aoFechar: () => void;
  aoSalvar: () => void;
}

const ESTADO_INICIAL = {
  tecnicasAplicadas: '',
  escalaDor: 0,
  evolucaoClinica: '',
  proximosPassos: '',
  pesoKg: '',
  temperaturaCelsius: '',
  frequenciaCardiaca: '',
  frequenciaRespiratoria: '',
  observacaoInterna: '',
};

/**
 * HU-008: formulário rápido de registro do atendimento fisioterapêutico.
 * HU-009: permite anexar uma observação interna, restrita à equipe clínica.
 * HU-010: anexa fotos e vídeos à sessão.
 */
export function ModalRegistrarAtendimento({ sessao, aoFechar, aoSalvar }: Props) {
  // O modal só existe quando há sessão escolhida: cada abertura monta um formulário
  // novo, o que dispensa um efeito para limpá-lo.
  if (!sessao) {
    return null;
  }

  return <Formulario sessao={sessao} aoFechar={aoFechar} aoSalvar={aoSalvar} />;
}

function Formulario({ sessao, aoFechar, aoSalvar }: Props & { sessao: ItemAgenda }) {
  const { podeVerObservacoesInternas } = useAuth();

  const [form, setForm] = useState(ESTADO_INICIAL);
  const [atendimentoSalvo, setAtendimentoSalvo] = useState<Atendimento | null>(null);
  const [midias, setMidias] = useState<Midia[]>([]);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [enviandoMidia, setEnviandoMidia] = useState(false);

  const campoArquivo = useRef<HTMLInputElement>(null);

  // A sessão pode já ter atendimento: nesse caso o modal abre em modo anexos.
  useEffect(() => {
    let ativo = true;

    async function carregarExistente() {
      try {
        const { data } = await api.get<Atendimento | null>(`/api/atendimentos/sessao/${sessao.sessaoId}`);

        if (!ativo) return;

        if (data) {
          setAtendimentoSalvo(data);
          setMidias(data.midias);
        } else {
          const { data: lista } = await api.get<Midia[]>(`/api/midias/sessao/${sessao.sessaoId}`);
          if (ativo) setMidias(lista);
        }
      } catch {
        if (ativo) setMidias([]);
      }
    }

    carregarExistente();

    return () => {
      ativo = false;
    };
  }, [sessao]);

  function atualizar<T extends keyof typeof ESTADO_INICIAL>(campo: T, valor: (typeof ESTADO_INICIAL)[T]) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();

    setErro('');

    if (!form.tecnicasAplicadas.trim() || !form.evolucaoClinica.trim()) {
      setErro('Informe as técnicas aplicadas e a evolução clínica.');
      return;
    }

    setSalvando(true);

    try {
      const { data } = await api.post<Atendimento>('/api/atendimentos', {
        sessaoId: sessao.sessaoId,
        tecnicasAplicadas: form.tecnicasAplicadas,
        escalaDor: form.escalaDor,
        evolucaoClinica: form.evolucaoClinica,
        proximosPassos: form.proximosPassos,
        // Os sinais vitais estruturados também viram um resumo em texto para a linha do tempo.
        sinaisVitais: montarResumoSinaisVitais(form),
        pesoKg: form.pesoKg ? Number(form.pesoKg) : null,
        temperaturaCelsius: form.temperaturaCelsius ? Number(form.temperaturaCelsius) : null,
        frequenciaCardiaca: form.frequenciaCardiaca ? Number(form.frequenciaCardiaca) : null,
        frequenciaRespiratoria: form.frequenciaRespiratoria ? Number(form.frequenciaRespiratoria) : null,
        observacaoInterna: form.observacaoInterna || null,
        concluirSessao: true,
      });

      setAtendimentoSalvo(data);
      setAviso('Atendimento registrado. Você ainda pode anexar fotos e vídeos desta sessão.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível registrar o atendimento.'));
    } finally {
      setSalvando(false);
    }
  }

  /** HU-010: upload de mídia vinculada à sessão. */
  async function enviarMidia(arquivo: File) {

    setErro('');
    setEnviandoMidia(true);

    const corpo = new FormData();
    corpo.append('sessaoId', sessao.sessaoId);
    corpo.append('arquivo', arquivo);

    if (atendimentoSalvo) {
      corpo.append('atendimentoId', atendimentoSalvo.id);
    }

    try {
      const { data } = await api.post<Midia>('/api/midias', corpo);
      setMidias((atual) => [data, ...atual]);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível anexar o arquivo.'));
    } finally {
      setEnviandoMidia(false);
      if (campoArquivo.current) campoArquivo.current.value = '';
    }
  }

  async function removerMidia(id: string) {
    try {
      await api.delete(`/api/midias/${id}`);
      setMidias((atual) => atual.filter((m) => m.id !== id));
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível remover o arquivo.'));
    }
  }

  return (
    <Modal
      aberto
      titulo={atendimentoSalvo ? 'Atendimento da sessão' : 'Registrar atendimento'}
      descricao={`${sessao.nomePaciente} · ${sessao.nomeTutor}`}
      aoFechar={() => {
        // Se algo foi registrado, a agenda precisa se atualizar ao fechar.
        if (atendimentoSalvo) aoSalvar();
        else aoFechar();
      }}
      largura="max-w-3xl"
    >
      {aviso && (
        <div className="mb-4">
          <Alerta tipo="sucesso">{aviso}</Alerta>
        </div>
      )}

      {atendimentoSalvo ? (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <ResumoCampo rotulo="Técnicas aplicadas" valor={atendimentoSalvo.tecnicasAplicadas} />
            <div>
              <p className="vc-rotulo">Escala de dor</p>
              <Etiqueta className={estiloEscalaDor(atendimentoSalvo.escalaDor)}>
                {atendimentoSalvo.escalaDor}/10
              </Etiqueta>
            </div>
            <ResumoCampo rotulo="Evolução clínica" valor={atendimentoSalvo.evolucaoClinica} />
            <ResumoCampo rotulo="Próximos passos" valor={atendimentoSalvo.proximosPassos} />
            <ResumoCampo rotulo="Sinais vitais" valor={atendimentoSalvo.sinaisVitais} />
          </div>
        </div>
      ) : (
        <form onSubmit={aoEnviar} className="space-y-4">
          <Campo rotulo="Técnicas aplicadas" obrigatorio dica="Separe por vírgula — o relatório usa esta lista no ranking de técnicas.">
            <input
              className="vc-campo"
              value={form.tecnicasAplicadas}
              onChange={(e) => atualizar('tecnicasAplicadas', e.target.value)}
              placeholder="Hidroterapia, Laserterapia, Cinesioterapia"
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
              />
              <Etiqueta className={estiloEscalaDor(form.escalaDor)}>{form.escalaDor}/10</Etiqueta>
            </div>
          </Campo>

          <Campo rotulo="Evolução clínica" obrigatorio>
            <textarea
              className="vc-campo"
              rows={3}
              value={form.evolucaoClinica}
              onChange={(e) => atualizar('evolucaoClinica', e.target.value)}
              placeholder="Descreva a resposta do paciente à sessão."
            />
          </Campo>

          <Campo rotulo="Próximos passos">
            <textarea
              className="vc-campo"
              rows={2}
              value={form.proximosPassos}
              onChange={(e) => atualizar('proximosPassos', e.target.value)}
            />
          </Campo>

          {/* HU-011, CA-3: o peso registrado aqui alimenta o gráfico de evolução. */}
          <fieldset className="rounded-xl border border-slate-200 p-4">
            <legend className="px-2 text-sm font-semibold text-slate-700">Sinais vitais</legend>

            <div className="grid gap-3 sm:grid-cols-4">
              <Campo rotulo="Peso (kg)">
                <input
                  type="number"
                  step="0.1"
                  min="0.1"
                  className="vc-campo"
                  value={form.pesoKg}
                  onChange={(e) => atualizar('pesoKg', e.target.value)}
                />
              </Campo>

              <Campo rotulo="Temp. (°C)">
                <input
                  type="number"
                  step="0.1"
                  className="vc-campo"
                  value={form.temperaturaCelsius}
                  onChange={(e) => atualizar('temperaturaCelsius', e.target.value)}
                />
              </Campo>

              <Campo rotulo="FC (bpm)">
                <input
                  type="number"
                  className="vc-campo"
                  value={form.frequenciaCardiaca}
                  onChange={(e) => atualizar('frequenciaCardiaca', e.target.value)}
                />
              </Campo>

              <Campo rotulo="FR (mpm)">
                <input
                  type="number"
                  className="vc-campo"
                  value={form.frequenciaRespiratoria}
                  onChange={(e) => atualizar('frequenciaRespiratoria', e.target.value)}
                />
              </Campo>
            </div>
          </fieldset>

          {/* HU-009 / RN-003: observação restrita, nunca visível ao tutor. */}
          {podeVerObservacoesInternas && (
            <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
              <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-amber-800">
                <Lock size={15} />
                Observação interna
              </div>
              <textarea
                className="vc-campo"
                rows={2}
                value={form.observacaoInterna}
                onChange={(e) => atualizar('observacaoInterna', e.target.value)}
                placeholder="Anotação restrita à equipe clínica."
              />
              <p className="mt-1.5 text-xs text-amber-700">
                Visível apenas para Administrador e Veterinário. O tutor nunca terá acesso a este conteúdo.
              </p>
            </div>
          )}

          {erro && <Alerta tipo="erro">{erro}</Alerta>}

          <div className="flex justify-end gap-2 pt-1">
            <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
              Cancelar
            </button>
            <button type="submit" className="vc-botao-primario" disabled={salvando}>
              {salvando && <Loader2 className="animate-spin" size={16} />}
              Salvar atendimento
            </button>
          </div>
        </form>
      )}

      {/* HU-010: anexos da sessão. */}
      <div className="mt-6 border-t border-slate-200 pt-5">
        <div className="mb-3 flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-slate-700">Fotos e vídeos da sessão</h3>

          <label className="vc-botao-secundario cursor-pointer">
            {enviandoMidia ? <Loader2 className="animate-spin" size={16} /> : <Paperclip size={16} />}
            Anexar
            <input
              ref={campoArquivo}
              type="file"
              className="sr-only"
              accept=".jpg,.jpeg,.png,.webp,.mp4,.mov"
              disabled={enviandoMidia}
              onChange={(e) => e.target.files?.[0] && enviarMidia(e.target.files[0])}
            />
          </label>
        </div>

        {erro && atendimentoSalvo && (
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
      <p className="text-sm text-slate-700">{valor}</p>
    </div>
  );
}

function montarResumoSinaisVitais(form: typeof ESTADO_INICIAL): string {
  const partes: string[] = [];

  if (form.frequenciaCardiaca) partes.push(`FC ${form.frequenciaCardiaca} bpm`);
  if (form.frequenciaRespiratoria) partes.push(`FR ${form.frequenciaRespiratoria} mpm`);
  if (form.temperaturaCelsius) partes.push(`Temp. ${form.temperaturaCelsius} °C`);
  if (form.pesoKg) partes.push(`Peso ${form.pesoKg} kg`);

  return partes.join(', ');
}
