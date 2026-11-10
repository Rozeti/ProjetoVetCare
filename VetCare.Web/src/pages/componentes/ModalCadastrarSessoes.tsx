import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { AlertTriangle, CalendarCheck, Loader2 } from 'lucide-react';
import { api, mensagemDeErro } from '../../services/api';
import { useAuth } from '../../contexts/auth';
import type { Clinica, PreviaDeSessoes, Sessao, Tratamento, Veterinario } from '../../types';
import { Alerta, Campo, Modal } from '../../components/ui';
import { formatarHora, formatarIntervalo, paraValorInputData } from '../../utils/formato';

interface Props {
  /** Tratamento que recebe as sessões; sem ele, o modal fica fechado. */
  tratamento: Tratamento | null;
  /** Paciente e tutor, para o cabeçalho. */
  descricao: string;
  /** Sessões do tratamento que continuam no plano (sem as canceladas e as inativadas). */
  cadastradas: Sessao[];
  aoFechar: () => void;
  aoSalvar: (mensagem: string) => void;
}

/** Mesmos limites da API (CadastrarSessoesDTO e observações da sessão). */
const MAXIMO_POR_CADASTRO = 60;
const TAMANHO_OBSERVACOES = 1000;

/** Segunda a domingo, com o número do dia em JavaScript (0 = domingo). */
const DIAS_DA_SEMANA: { dia: number; curto: string; nome: string }[] = [
  { dia: 1, curto: 'Seg', nome: 'segunda' },
  { dia: 2, curto: 'Ter', nome: 'terça' },
  { dia: 3, curto: 'Qua', nome: 'quarta' },
  { dia: 4, curto: 'Qui', nome: 'quinta' },
  { dia: 5, curto: 'Sex', nome: 'sexta' },
  { dia: 6, curto: 'Sáb', nome: 'sábado' },
  { dia: 0, curto: 'Dom', nome: 'domingo' },
];

/** Soma dias a uma data "AAAA-MM-DD" sem passar por fuso horário. */
function somarDias(data: string, dias: number): string {
  const dia = new Date(`${data}T12:00:00`);
  dia.setDate(dia.getDate() + dias);
  return paraValorInputData(dia);
}

function diaDaSemana(data: string): number {
  return new Date(`${data}T12:00:00`).getDay();
}

/** "09:00" + 60 minutos = "10:00"; passando da meia-noite, fica vazio. */
function somarMinutos(hora: string, minutos: number): string {
  const [h, m] = hora.split(':').map(Number);
  const total = h * 60 + m + minutos;
  if (Number.isNaN(total) || total >= 24 * 60) return '';
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
}

const FORMATO_CURTO: Intl.DateTimeFormatOptions = { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' };

/** "seg., 16/11/2026" de um instante (início de uma sessão). */
function dataDoInstante(valor: string): string {
  return new Date(valor).toLocaleDateString('pt-BR', FORMATO_CURTO);
}

/** "seg., 16/11/2026" de um dia do calendário da clínica ("AAAA-MM-DD..."), sem passar por fuso. */
function dataDoDia(valor: string): string {
  return new Date(`${valor.slice(0, 10)}T12:00:00`).toLocaleDateString('pt-BR', FORMATO_CURTO);
}

/** "segundas e quartas" */
function descreverDias(dias: Set<number>): string {
  const nomes = DIAS_DA_SEMANA.filter((d) => dias.has(d.dia)).map((d) => `${d.nome}s`);
  if (nomes.length === 0) return '';
  return nomes.length === 1 ? nomes[0] : `${nomes.slice(0, -1).join(', ')} e ${nomes[nomes.length - 1]}`;
}

/**
 * Sugestões a partir das sessões já cadastradas: o mesmo horário e os mesmos dias da semana, a
 * partir do dia seguinte à última sessão. Sem sessões, amanhã, no dia da semana de amanhã.
 */
function sugestoes(cadastradas: Sessao[]) {
  const amanha = somarDias(paraValorInputData(new Date()), 1);
  const ordenadas = [...cadastradas].sort((a, b) => a.dataHora.localeCompare(b.dataHora));
  const ultima = ordenadas[ordenadas.length - 1];

  if (!ultima) {
    return { aPartirDe: amanha, inicio: '', fim: '', dias: new Set([diaDaSemana(amanha)]) };
  }

  const depoisDaUltima = somarDias(paraValorInputData(ultima.dataHora), 1);
  const recentes = ordenadas.slice(-6);

  return {
    aPartirDe: depoisDaUltima > amanha ? depoisDaUltima : amanha,
    inicio: formatarHora(ultima.dataHora),
    fim: ultima.dataHoraFim ? formatarHora(ultima.dataHoraFim) : '',
    dias: new Set(recentes.map((s) => new Date(s.dataHora).getDay())),
  };
}

/**
 * HU-004: cadastro das sessões de um tratamento pelo plano — quantas sessões, a partir de
 * quando, em quais dias da semana e em que horário. A API monta as datas (pulando os dias sem
 * agenda) e mostra a prévia antes de gravar. O mesmo formulário cadastra o plano inicial e
 * acrescenta sessões depois, no meio ou no fim do tratamento.
 */
export function ModalCadastrarSessoes({ tratamento, ...props }: Props) {
  // Fechado, o modal não existe: cada abertura começa das sugestões do momento.
  if (!tratamento) return null;

  return <Formulario tratamento={tratamento} {...props} />;
}

function Formulario({ tratamento, descricao, cadastradas, aoFechar, aoSalvar }: Props & { tratamento: Tratamento }) {
  const { usuario, ehVeterinario } = useAuth();
  const sugerido = useMemo(() => sugestoes(cadastradas), [cadastradas]);
  const primeiroCadastro = cadastradas.length === 0;
  const faltam = tratamento.sessoesPrevistas - cadastradas.length;

  const [clinica, setClinica] = useState<Clinica | null>(null);
  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  // O veterinário cadastra na própria agenda; a administração e a recepção escolhem a de quem vai atender.
  const [veterinarioId, setVeterinarioId] = useState(ehVeterinario ? usuario?.veterinarioId ?? '' : tratamento.veterinarioId);

  const [quantidade, setQuantidade] = useState(String(faltam > 0 ? Math.min(faltam, MAXIMO_POR_CADASTRO) : 1));
  const [aPartirDe, setAPartirDe] = useState(sugerido.aPartirDe);
  const [dias, setDias] = useState<Set<number>>(sugerido.dias);
  const [inicio, setInicio] = useState(sugerido.inicio);
  const [fim, setFim] = useState(sugerido.fim);
  const [observacoes, setObservacoes] = useState('');

  const [resposta, setResposta] = useState<{ consulta: string; previa: PreviaDeSessoes | null; erro: string } | null>(null);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const duracao = clinica?.duracaoSessaoMinutos ?? 60;
  const numero = Number(quantidade);

  useEffect(() => {
    let ativo = true;

    api
      .get<Clinica>('/api/clinica')
      .then(({ data }) => ativo && setClinica(data))
      .catch(() => undefined);

    if (!ehVeterinario) {
      api
        .get<Veterinario[]>('/api/veterinarios')
        .then(({ data }) => ativo && setVeterinarios(data.filter((v) => v.ativo)))
        .catch((falha) => ativo && setErro(mensagemDeErro(falha, 'Não foi possível carregar os veterinários.')));
    }

    return () => {
      ativo = false;
    };
  }, [ehVeterinario]);

  // O que falta preencher antes de a prévia fazer sentido; null quando está tudo certo.
  const pendencia = !Number.isInteger(numero) || numero < 1 || numero > MAXIMO_POR_CADASTRO
    ? `Informe de 1 a ${MAXIMO_POR_CADASTRO} sessões.`
    : !aPartirDe
      ? 'Informe a partir de qual data.'
      : dias.size === 0
        ? 'Escolha ao menos um dia da semana.'
        : !inicio || !fim
          ? 'Informe o horário de início e o de término.'
          : fim <= inicio
            ? 'O término precisa ser depois do início.'
            : !veterinarioId
              ? 'Escolha o veterinário que vai atender.'
              : null;

  // A consulta da prévia, como texto: muda a cada alteração do plano e identifica a resposta.
  const consulta = pendencia
    ? null
    : JSON.stringify({
        tratamentoId: tratamento.id,
        veterinarioId,
        quantidade: numero,
        aPartirDe,
        horaInicio: inicio,
        horaFim: fim,
        diasDaSemana: [...dias].sort(),
      });

  // A prévia é recalculada pela API a cada mudança, com uma pequena espera enquanto se digita.
  useEffect(() => {
    if (!consulta) return;

    let ativo = true;
    const espera = window.setTimeout(() => {
      api
        .get<PreviaDeSessoes>('/api/sessoes/previa', { params: JSON.parse(consulta), paramsSerializer: { indexes: null } })
        .then(({ data }) => ativo && setResposta({ consulta, previa: data, erro: '' }))
        .catch(
          (falha) =>
            ativo &&
            setResposta({ consulta, previa: null, erro: mensagemDeErro(falha, 'Não foi possível montar as datas das sessões.') }),
        );
    }, 350);

    return () => {
      ativo = false;
      window.clearTimeout(espera);
    };
  }, [consulta]);

  // Só vale a resposta da consulta atual; enquanto ela não chega, a prévia está sendo calculada.
  const atual = resposta && resposta.consulta === consulta ? resposta : null;
  const previa = atual?.previa ?? null;
  const erroPrevia = atual?.erro ?? '';
  const calculando = consulta !== null && atual === null;

  function alternarDia(dia: number) {
    setDias((atual) => {
      const novo = new Set(atual);
      if (novo.has(dia)) novo.delete(dia);
      else novo.add(dia);
      return novo;
    });
  }

  function mudarInicio(valor: string) {
    setInicio(valor);
    // O término acompanha o início pela duração padrão da clínica, e continua editável.
    if (valor) setFim(somarMinutos(valor, duracao));
  }

  function mudarAPartirDe(valor: string) {
    setAPartirDe(valor);
    // Sem dia da semana escolhido, o da data informada é o ponto de partida.
    if (valor && dias.size === 0) setDias(new Set([diaDaSemana(valor)]));
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro('');

    if (pendencia) {
      setErro(pendencia);
      return;
    }

    if (!previa) {
      setErro(erroPrevia || 'Aguarde a prévia das datas.');
      return;
    }

    setSalvando(true);

    try {
      const { data } = await api.post<Sessao[]>('/api/sessoes', {
        tratamentoId: tratamento.id,
        veterinarioId,
        observacoes: observacoes.trim(),
        sessoes: previa.sessoes,
      });

      const ampliou = previa.novoTotalPrevisto > previa.sessoesPrevistas;
      aoSalvar(
        (data.length === 1 ? 'Sessão cadastrada.' : `${data.length} sessões cadastradas.`) +
          (ampliou ? ` O tratamento passa a prever ${previa.novoTotalPrevisto} sessões.` : ''),
      );
    } catch (falha) {
      // Entre a prévia e o cadastro alguém pode ter ocupado um horário: a API confere de novo.
      setErro(mensagemDeErro(falha, 'Não foi possível cadastrar as sessões.'));
    } finally {
      setSalvando(false);
    }
  }

  const ampliacao = previa && previa.novoTotalPrevisto > previa.sessoesPrevistas;

  return (
    <Modal
      aberto
      titulo={primeiroCadastro ? 'Cadastrar sessões' : 'Adicionar sessões'}
      descricao={`${tratamento.nome} · ${descricao}`}
      aoFechar={aoFechar}
      largura="max-w-4xl"
    >
      <form onSubmit={aoEnviar} noValidate>
        <p className="mb-5 rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <strong className="font-semibold text-slate-800">Plano do tratamento:</strong> {tratamento.sessoesPrevistas}{' '}
          {tratamento.sessoesPrevistas === 1 ? 'sessão prevista' : 'sessões previstas'} · {cadastradas.length}{' '}
          {cadastradas.length === 1 ? 'cadastrada' : 'cadastradas'}
          {faltam > 0 ? ` · faltam ${faltam}` : ' · plano completo'}
          {tratamento.frequencia && ` · frequência: ${tratamento.frequencia}`}
        </p>

        <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
          {/* O plano: quantas, a partir de quando, em que dias e em que horário. */}
          <div className="space-y-4">
            <Campo
              rotulo="Quantidade de sessões"
              obrigatorio
              dica={
                faltam > 0
                  ? `Faltam ${faltam} para completar o plano; mais do que isso amplia o tratamento.`
                  : 'O plano já está completo: as novas sessões ampliam o tratamento.'
              }
            >
              <input
                type="number"
                min={1}
                max={MAXIMO_POR_CADASTRO}
                className="vc-campo"
                value={quantidade}
                onChange={(e) => setQuantidade(e.target.value)}
              />
            </Campo>

            <Campo rotulo="A partir de" obrigatorio>
              <input
                type="date"
                className="vc-campo"
                min={paraValorInputData(new Date())}
                value={aPartirDe}
                onChange={(e) => mudarAPartirDe(e.target.value)}
              />
            </Campo>

            <fieldset>
              <legend className="vc-rotulo">
                Dias da semana<span className="ml-0.5 text-perigo">*</span>
              </legend>
              <div className="flex flex-wrap gap-1.5">
                {DIAS_DA_SEMANA.map(({ dia, curto, nome }) => {
                  const marcado = dias.has(dia);

                  return (
                    <button
                      key={dia}
                      type="button"
                      aria-pressed={marcado}
                      aria-label={nome}
                      onClick={() => alternarDia(dia)}
                      className={`min-w-11 rounded-lg px-2.5 py-1.5 text-sm font-semibold transition ${
                        marcado ? 'bg-brand text-white' : 'bg-white text-slate-600 ring-1 ring-slate-300 hover:bg-slate-50'
                      }`}
                    >
                      {curto}
                    </button>
                  );
                })}
              </div>
            </fieldset>

            <div className="grid grid-cols-2 gap-3">
              <Campo rotulo="Início" obrigatorio>
                <input type="time" className="vc-campo" value={inicio} onChange={(e) => mudarInicio(e.target.value)} />
              </Campo>
              <Campo rotulo="Término" obrigatorio>
                <input type="time" className="vc-campo" value={fim} onChange={(e) => setFim(e.target.value)} />
              </Campo>
            </div>
            {clinica && (
              <p className="-mt-2 text-xs text-slate-500">
                A clínica atende das {clinica.horarioAbertura} às {clinica.horarioFechamento}. O término é sugerido pela
                duração padrão de {duracao} minutos.
              </p>
            )}

            {!ehVeterinario && (
              <Campo rotulo="Veterinário" obrigatorio dica="As sessões entram na agenda deste profissional.">
                <select className="vc-campo" value={veterinarioId} onChange={(e) => setVeterinarioId(e.target.value)}>
                  <option value="">Selecione o profissional</option>
                  {veterinarios.map((vet) => (
                    <option key={vet.id} value={vet.id}>
                      {vet.nome}
                    </option>
                  ))}
                </select>
              </Campo>
            )}

            <Campo rotulo="Observações para o tutor" dica={`Opcional; vão em todas as sessões. ${observacoes.length}/${TAMANHO_OBSERVACOES}`}>
              <textarea
                className="vc-campo"
                rows={2}
                value={observacoes}
                onChange={(e) => setObservacoes(e.target.value)}
                placeholder="Ex.: trazer o pet em jejum de duas horas."
                maxLength={TAMANHO_OBSERVACOES}
              />
            </Campo>
          </div>

          {/* A prévia: as datas exatas que serão cadastradas. */}
          <section aria-live="polite" className="flex min-h-64 flex-col rounded-xl border border-slate-200">
            <header className="flex items-center justify-between gap-2 border-b border-slate-100 px-4 py-3">
              <h3 className="text-sm font-semibold text-slate-800">Datas das sessões</h3>
              {calculando && <Loader2 size={15} className="animate-spin text-slate-400" aria-label="Calculando" />}
            </header>

            <div className="max-h-96 flex-1 overflow-y-auto px-4 py-3">
              {pendencia ? (
                <p className="text-sm text-slate-500">{pendencia}</p>
              ) : erroPrevia ? (
                <p className="text-sm text-perigo" role="alert">
                  {erroPrevia}
                </p>
              ) : previa ? (
                <>
                  <p className="mb-2 text-xs text-slate-500">
                    {previa.sessoes.length} {previa.sessoes.length === 1 ? 'sessão' : 'sessões'}, às {descreverDias(dias)}, das{' '}
                    {inicio} às {fim}.
                  </p>

                  {/* Os dias pulados vêm antes da lista: é o que precisa da atenção de quem cadastra. */}
                  {previa.puladas.length > 0 && (
                    <div className="mb-3 rounded-lg bg-alerta-claro px-3 py-2 text-xs text-amber-900">
                      <p className="mb-1 flex items-center gap-1 font-semibold">
                        <AlertTriangle size={13} aria-hidden="true" />
                        {previa.puladas.length === 1 ? 'Um dia foi pulado' : `${previa.puladas.length} dias foram pulados`}
                      </p>
                      <ul className="space-y-0.5">
                        {previa.puladas.map((pulada) => (
                          <li key={pulada.data}>
                            <span className="capitalize">{dataDoDia(pulada.data)}</span>: {pulada.motivo}.
                          </li>
                        ))}
                      </ul>
                      <p className="mt-1">As sessões seguem para os próximos dias escolhidos.</p>
                    </div>
                  )}

                  <ol className="divide-y divide-slate-100">
                    {previa.sessoes.map((sessao, indice) => (
                      <li key={sessao.inicio} className="flex items-center gap-3 py-1.5 text-sm">
                        <span className="w-6 text-right text-xs font-semibold tabular-nums text-slate-400">{indice + 1}.</span>
                        <span className="flex-1 capitalize text-slate-800">{dataDoInstante(sessao.inicio)}</span>
                        <span className="tabular-nums text-slate-600">{formatarIntervalo(sessao.inicio, sessao.fim)}</span>
                      </li>
                    ))}
                  </ol>
                </>
              ) : (
                <p className="text-sm text-slate-500">Calculando as datas...</p>
              )}
            </div>

            {ampliacao && (
              <p className="border-t border-slate-100 px-4 py-2 text-xs text-slate-600">
                O tratamento passa de {previa.sessoesPrevistas} para <strong>{previa.novoTotalPrevisto}</strong> sessões previstas.
              </p>
            )}
          </section>
        </div>

        {erro && (
          <div className="mt-5">
            <Alerta tipo="erro">{erro}</Alerta>
          </div>
        )}

        <div className="mt-6 flex justify-end gap-2">
          <button type="button" className="vc-botao-secundario" onClick={aoFechar} disabled={salvando}>
            Cancelar
          </button>
          <button type="submit" className="vc-botao-primario" disabled={salvando || calculando || !previa}>
            {salvando ? <Loader2 className="animate-spin" size={16} /> : <CalendarCheck size={16} />}
            {previa
              ? previa.sessoes.length === 1
                ? 'Cadastrar 1 sessão'
                : `Cadastrar ${previa.sessoes.length} sessões`
              : 'Cadastrar sessões'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
