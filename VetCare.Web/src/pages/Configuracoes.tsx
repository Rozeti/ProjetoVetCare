import { useEffect, useState, type FormEvent } from 'react';
import { Loader2, Save } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import type { Clinica } from '../types';
import { Alerta, CabecalhoPagina, Campo, Card, Carregando } from '../components/ui';

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
    </>
  );
}
