import { useEffect, useState, type FormEvent } from 'react';
import { Loader2, MailCheck, Save, Send, ServerOff } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import type { Clinica, SituacaoEmail, TesteDeEmail } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando, Etiqueta } from '../components/ui';

/**
 * Parâmetros operacionais da clínica. Os valores aqui alimentam regras de negócio:
 * a antecedência de cancelamento (RN-009) e a janela usada na checagem de conflito
 * de horário (RN-002).
 */
export function Configuracoes() {
  const [clinica, setClinica] = useState<Clinica | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');

  useEffect(() => {
    api
      .get<Clinica>('/api/clinica')
      .then(({ data }) => setClinica(data))
      .catch((falha) => setErro(mensagemDeErro(falha, 'Não foi possível carregar as configurações.')))
      .finally(() => setCarregando(false));
  }, []);

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    if (!clinica) return;

    setErro('');
    setAviso('');
    setSalvando(true);

    try {
      await api.put('/api/clinica', {
        nome: clinica.nome,
        cnpj: clinica.cnpj,
        telefone: clinica.telefone,
        endereco: clinica.endereco,
        horasMinimasCancelamento: clinica.horasMinimasCancelamento,
        duracaoSessaoMinutos: clinica.duracaoSessaoMinutos,
        horarioAbertura: clinica.horarioAbertura,
        horarioFechamento: clinica.horarioFechamento,
      });

      setAviso('Configurações salvas com sucesso.');
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível salvar as configurações.'));
    } finally {
      setSalvando(false);
    }
  }

  function atualizar<T extends keyof Clinica>(campo: T, valor: Clinica[T]) {
    setClinica((atual) => (atual ? { ...atual, [campo]: valor } : atual));
  }

  if (carregando) {
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

      <form onSubmit={aoEnviar} className="max-w-3xl space-y-6">
        {erro && <Alerta tipo="erro">{erro}</Alerta>}
        {aviso && <Alerta tipo="sucesso">{aviso}</Alerta>}

        <Card className="p-6">
          <h2 className="mb-4 font-semibold text-slate-900">Dados da clínica</h2>

          <div className="space-y-4">
            <Campo rotulo="Nome" obrigatorio>
              <input className="vc-campo" value={clinica.nome} onChange={(e) => atualizar('nome', e.target.value)} />
            </Campo>

            <div className="grid gap-4 sm:grid-cols-2">
              <Campo rotulo="CNPJ">
                <input className="vc-campo" value={clinica.cnpj} onChange={(e) => atualizar('cnpj', e.target.value)} />
              </Campo>

              <Campo rotulo="Telefone">
                <input
                  className="vc-campo"
                  value={clinica.telefone}
                  onChange={(e) => atualizar('telefone', e.target.value)}
                />
              </Campo>
            </div>

            <Campo rotulo="Endereço">
              <input
                className="vc-campo"
                value={clinica.endereco}
                onChange={(e) => atualizar('endereco', e.target.value)}
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
            <Campo rotulo="Abertura">
              <input
                type="time"
                className="vc-campo"
                value={clinica.horarioAbertura}
                onChange={(e) => atualizar('horarioAbertura', e.target.value)}
              />
            </Campo>

            <Campo rotulo="Fechamento">
              <input
                type="time"
                className="vc-campo"
                value={clinica.horarioFechamento}
                onChange={(e) => atualizar('horarioFechamento', e.target.value)}
              />
            </Campo>

            <Campo
              rotulo="Duração da sessão (minutos)"
              dica="Usado para detectar sobreposição de horários na agenda do veterinário."
            >
              <input
                type="number"
                min={15}
                max={240}
                step={15}
                className="vc-campo"
                value={clinica.duracaoSessaoMinutos}
                onChange={(e) => atualizar('duracaoSessaoMinutos', Number(e.target.value))}
              />
            </Campo>

            <Campo
              rotulo="Antecedência para cancelamento (horas)"
              dica="Prazo mínimo que o tutor deve respeitar para cancelar uma sessão pelo aplicativo."
            >
              <input
                type="number"
                min={0}
                max={168}
                className="vc-campo"
                value={clinica.horasMinimasCancelamento}
                onChange={(e) => atualizar('horasMinimasCancelamento', Number(e.target.value))}
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

      <div className="mt-6 max-w-3xl">
        <CartaoDeEmail />
      </div>
    </>
  );
}

/**
 * Situação do canal de e-mail e envio de teste. O servidor é definido no `.env` da
 * API (não há como trocá-lo pela tela, de propósito: a senha do SMTP não deve passar
 * pelo navegador), mas o administrador consegue conferir aqui se ele está funcionando.
 */
function CartaoDeEmail() {
  const [situacao, setSituacao] = useState<SituacaoEmail | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');
  const [testando, setTestando] = useState(false);
  const [resultado, setResultado] = useState<TesteDeEmail | null>(null);
  const [erroDoTeste, setErroDoTeste] = useState('');

  useEffect(() => {
    api
      .get<SituacaoEmail>('/api/email')
      .then(({ data }) => setSituacao(data))
      .catch((falha) => setErro(mensagemDeErro(falha, 'Não foi possível consultar a situação do e-mail.')))
      .finally(() => setCarregando(false));
  }, []);

  async function enviarTeste() {
    setResultado(null);
    setErroDoTeste('');
    setTestando(true);

    try {
      const { data } = await api.post<TesteDeEmail>('/api/email/teste');
      setResultado(data);
    } catch (falha) {
      setErroDoTeste(mensagemDeErro(falha, 'Não foi possível enviar o e-mail de teste.'));
    } finally {
      setTestando(false);
    }
  }

  return (
    <Card className="p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="mb-1 font-semibold text-slate-900">Envio de e-mails</h2>
          <p className="text-sm text-slate-500">
            Recuperação de senha, boas-vindas e avisos do tratamento saem por este canal.
          </p>
        </div>

        {situacao && (
          <Etiqueta
            className={
              situacao.enviaDeVerdade ? 'bg-sucesso-claro text-emerald-800' : 'bg-alerta-claro text-amber-800'
            }
          >
            {situacao.enviaDeVerdade ? <MailCheck size={14} /> : <ServerOff size={14} />}
            {situacao.enviaDeVerdade ? 'Servidor configurado' : 'Sem servidor de e-mail'}
          </Etiqueta>
        )}
      </div>

      {carregando ? (
        <Carregando texto="Consultando o canal de e-mail..." />
      ) : erro || !situacao ? (
        <div className="mt-4">
          <Alerta tipo="erro">{erro || 'Situação do e-mail indisponível.'}</Alerta>
        </div>
      ) : (
        <>
          <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
            <div className="rounded-xl bg-slate-50 px-4 py-3">
              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Servidor</dt>
              <dd className="mt-0.5 break-all font-medium text-slate-800">
                {situacao.servidor ?? 'Nenhum — e-mails gravados na pasta emails-enviados'}
              </dd>
            </div>

            <div className="rounded-xl bg-slate-50 px-4 py-3">
              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Remetente</dt>
              <dd className="mt-0.5 break-all font-medium text-slate-800">
                {situacao.nomeRemetente} &lt;{situacao.remetente}&gt;
              </dd>
            </div>
          </dl>

          {!situacao.enviaDeVerdade && (
            <div className="mt-4">
              <Alerta tipo="aviso">
                Nenhum servidor de e-mail está configurado, então as mensagens não chegam a ninguém: elas ficam
                gravadas na pasta <strong>emails-enviados</strong> da API. Para enviar de verdade, preencha{' '}
                <strong>SMTP_HOST</strong>, <strong>SMTP_USUARIO</strong> e <strong>SMTP_SENHA</strong> no
                arquivo <strong>.env</strong> e suba a API novamente. O guia COMO-USAR explica o passo a passo
                com o Gmail.
              </Alerta>
            </div>
          )}

          {resultado && (
            <div className="mt-4">
              <Alerta tipo="sucesso" aoFechar={() => setResultado(null)}>
                {resultado.mensagem}
              </Alerta>
            </div>
          )}

          {erroDoTeste && (
            <div className="mt-4">
              <Alerta tipo="erro" aoFechar={() => setErroDoTeste('')}>
                {erroDoTeste}
              </Alerta>
            </div>
          )}

          <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4">
            <p className="text-xs text-slate-500">
              O e-mail de teste vai para o endereço da sua própria conta e responde na hora se o servidor aceitou.
            </p>

            <button type="button" className="vc-botao-secundario" onClick={enviarTeste} disabled={testando}>
              {testando ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}
              Enviar e-mail de teste
            </button>
          </div>
        </>
      )}
    </Card>
  );
}
