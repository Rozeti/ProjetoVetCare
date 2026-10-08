import { useCallback, useState, type FormEvent } from 'react';
import { Loader2, Save } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useConfirmacao } from '../hooks/useConfirmacao';
import { useCarregamento } from '../hooks/useCarregamento';
import { useAtualizacao } from '../contexts/atualizacoes';
import type { Clinica } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando } from '../components/ui';

/** Mesmos limites de AtualizarClinicaDTO. */
const LIMITES = {
  nome: 150,
  cnpj: 20,
  telefone: 30,
  endereco: 250,
  duracaoMinima: 15,
  duracaoMaxima: 240,
  antecedenciaMaxima: 168,
};

/** "HH:mm" em minutos, ou null quando o campo está vazio ou mal formado. */
function minutos(horario: string): number | null {
  const partes = /^(\d{2}):(\d{2})$/.exec(horario);

  if (!partes) return null;

  return Number(partes[1]) * 60 + Number(partes[2]);
}

/**
 * Parâmetros operacionais da clínica. Os valores aqui alimentam regras de negócio:
 * a antecedência de cancelamento (RN-009) e a janela usada na checagem de conflito
 * de horário (RN-002).
 */
export function Configuracoes() {
  const [clinica, setClinica] = useState<Clinica | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState('');
  const [aviso, setAviso] = useState('');

  const buscar = useCallback(async () => {
    const { data } = await api.get<Clinica>('/api/clinica');
    setClinica(data);
    return data;
  }, []);

  const { carregando, erro, recarregar } = useCarregamento(buscar, 'Não foi possível carregar as configurações.');

  // Dois administradores editando ao mesmo tempo: o segundo vê a versão salva pelo primeiro.
  useAtualizacao(['clinica'], () => {
    setAviso('As configurações foram alteradas por outro usuário; a tela foi atualizada.');
    recarregar();
  });

  const { confirmarEdicao } = useConfirmacao();

  function validar(dados: Clinica): string | null {
    const nome = dados.nome.trim();

    if (nome.length < 2 || nome.length > LIMITES.nome) {
      return `Informe o nome da clínica (entre 2 e ${LIMITES.nome} caracteres).`;
    }

    if (dados.cnpj.trim().length > LIMITES.cnpj) return `O CNPJ deve ter até ${LIMITES.cnpj} caracteres.`;
    if (dados.telefone.trim().length > LIMITES.telefone) return `O telefone deve ter até ${LIMITES.telefone} caracteres.`;
    if (dados.endereco.trim().length > LIMITES.endereco) return `O endereço deve ter até ${LIMITES.endereco} caracteres.`;

    const abertura = minutos(dados.horarioAbertura);
    const fechamento = minutos(dados.horarioFechamento);

    if (abertura == null || fechamento == null) return 'Informe os horários de abertura e fechamento.';
    if (fechamento <= abertura) return 'O horário de fechamento deve ser posterior ao de abertura.';

    if (
      !Number.isInteger(dados.duracaoSessaoMinutos) ||
      dados.duracaoSessaoMinutos < LIMITES.duracaoMinima ||
      dados.duracaoSessaoMinutos > LIMITES.duracaoMaxima
    ) {
      return `A duração da sessão deve ficar entre ${LIMITES.duracaoMinima} e ${LIMITES.duracaoMaxima} minutos.`;
    }

    if (fechamento - abertura < dados.duracaoSessaoMinutos) {
      return 'O expediente precisa comportar pelo menos uma sessão com a duração informada.';
    }

    if (
      !Number.isInteger(dados.horasMinimasCancelamento) ||
      dados.horasMinimasCancelamento < 0 ||
      dados.horasMinimasCancelamento > LIMITES.antecedenciaMaxima
    ) {
      return `A antecedência de cancelamento deve ficar entre 0 e ${LIMITES.antecedenciaMaxima} horas.`;
    }

    return null;
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    if (!clinica) return;

    setErroForm('');
    setAviso('');

    const problema = validar(clinica);

    if (problema) {
      setErroForm(problema);
      return;
    }

    if (
      !(await confirmarEdicao(
        'Salvar as configurações da clínica? Os novos horários e prazos passam a valer imediatamente para a agenda.',
        'Salvar configurações',
      ))
    ) {
      return;
    }

    setSalvando(true);

    try {
      const { data } = await api.put<Clinica>('/api/clinica', {
        nome: clinica.nome.trim(),
        cnpj: clinica.cnpj.trim(),
        telefone: clinica.telefone.trim(),
        endereco: clinica.endereco.trim(),
        horasMinimasCancelamento: clinica.horasMinimasCancelamento,
        duracaoSessaoMinutos: clinica.duracaoSessaoMinutos,
        horarioAbertura: clinica.horarioAbertura,
        horarioFechamento: clinica.horarioFechamento,
      });

      setClinica(data);
      setAviso('Configurações salvas com sucesso.');
    } catch (falha) {
      setErroForm(mensagemDeErro(falha, 'Não foi possível salvar as configurações.'));
    } finally {
      setSalvando(false);
    }
  }

  function atualizar<T extends keyof Clinica>(campo: T, valor: Clinica[T]) {
    setClinica((atual) => (atual ? { ...atual, [campo]: valor } : atual));
  }

  /** Um campo numérico apagado vira 0 em `Number('')`; aqui ele mantém o último valor válido. */
  function atualizarNumero(campo: 'duracaoSessaoMinutos' | 'horasMinimasCancelamento', texto: string) {
    if (texto === '') return;

    const valor = Number(texto);

    if (Number.isFinite(valor)) {
      atualizar(campo, Math.trunc(valor));
    }
  }

  if (carregando && !clinica) {
    return <Carregando texto="Carregando configurações..." />;
  }

  if (!clinica) {
    return <Alerta tipo="erro">{erro || 'Clínica não encontrada.'}</Alerta>;
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Configurações da clínica"
        descricao="Dados institucionais e parâmetros que regem o funcionamento da agenda."
      />

      <form onSubmit={aoEnviar} className="max-w-3xl space-y-6" noValidate>
        {(erro || erroForm) && <Alerta tipo="erro">{erroForm || erro}</Alerta>}
        {aviso && (
          <Alerta tipo="sucesso" aoFechar={() => setAviso('')}>
            {aviso}
          </Alerta>
        )}

        <Card className="p-6">
          <h2 className="mb-4 font-semibold text-slate-900">Dados da clínica</h2>

          <div className="space-y-4">
            <Campo rotulo="Nome" obrigatorio dica="Aparece no rodapé do portal, nos e-mails e nos documentos impressos.">
              <input
                className="vc-campo"
                value={clinica.nome}
                onChange={(e) => atualizar('nome', e.target.value)}
                maxLength={LIMITES.nome}
              />
            </Campo>

            <div className="grid gap-4 sm:grid-cols-2">
              <Campo rotulo="CNPJ">
                <input
                  className="vc-campo"
                  value={clinica.cnpj}
                  onChange={(e) => atualizar('cnpj', e.target.value)}
                  maxLength={LIMITES.cnpj}
                  placeholder="00.000.000/0000-00"
                />
              </Campo>

              <Campo rotulo="Telefone">
                <input
                  className="vc-campo"
                  value={clinica.telefone}
                  onChange={(e) => atualizar('telefone', e.target.value)}
                  maxLength={LIMITES.telefone}
                  autoComplete="tel"
                />
              </Campo>
            </div>

            <Campo rotulo="Endereço">
              <input
                className="vc-campo"
                value={clinica.endereco}
                onChange={(e) => atualizar('endereco', e.target.value)}
                maxLength={LIMITES.endereco}
                autoComplete="street-address"
              />
            </Campo>
          </div>
        </Card>

        <Card className="p-6">
          <h2 className="mb-1 font-semibold text-slate-900">Regras da agenda</h2>
          <p className="mb-4 text-sm text-slate-500">
            Estes valores são aplicados diretamente pelo sistema ao agendar e ao cancelar sessões.
          </p>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo rotulo="Abertura" obrigatorio>
              <input
                type="time"
                className="vc-campo"
                value={clinica.horarioAbertura}
                onChange={(e) => atualizar('horarioAbertura', e.target.value)}
              />
            </Campo>

            <Campo rotulo="Fechamento" obrigatorio dica="Precisa ser depois da abertura.">
              <input
                type="time"
                className="vc-campo"
                min={clinica.horarioAbertura}
                value={clinica.horarioFechamento}
                onChange={(e) => atualizar('horarioFechamento', e.target.value)}
              />
            </Campo>

            <Campo
              rotulo="Duração da sessão (minutos)"
              obrigatorio
              dica={`Entre ${LIMITES.duracaoMinima} e ${LIMITES.duracaoMaxima}. Usado para detectar sobreposição de horários na agenda.`}
            >
              <input
                type="number"
                min={LIMITES.duracaoMinima}
                max={LIMITES.duracaoMaxima}
                step={5}
                className="vc-campo"
                value={clinica.duracaoSessaoMinutos}
                onChange={(e) => atualizarNumero('duracaoSessaoMinutos', e.target.value)}
              />
            </Campo>

            <Campo
              rotulo="Antecedência para cancelamento (horas)"
              obrigatorio
              dica={`Entre 0 e ${LIMITES.antecedenciaMaxima}. Prazo mínimo que o tutor deve respeitar para cancelar pelo aplicativo.`}
            >
              <input
                type="number"
                min={0}
                max={LIMITES.antecedenciaMaxima}
                className="vc-campo"
                value={clinica.horasMinimasCancelamento}
                onChange={(e) => atualizarNumero('horasMinimasCancelamento', e.target.value)}
              />
            </Campo>
          </div>
        </Card>

        <div className="flex justify-end">
          <button type="submit" className="vc-botao-primario" disabled={salvando}>
            {salvando ? <Loader2 className="animate-spin" size={16} /> : <Save size={16} />}
            Salvar configurações
          </button>
        </div>
      </form>
    </>
  );
}
