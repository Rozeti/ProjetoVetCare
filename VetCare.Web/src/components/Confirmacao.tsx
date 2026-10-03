import { useCallback, useRef, useState, type ReactNode } from 'react';
import { AlertTriangle, HelpCircle } from 'lucide-react';
import { Modal } from './ui';
import {
  ContextoDeConfirmacao,
  type OpcoesDeConfirmacao,
  type Perguntar,
  type RespostaDeConfirmacao,
} from '../hooks/useConfirmacao';

interface Pendente {
  opcoes: OpcoesDeConfirmacao;
  responder: (resposta: RespostaDeConfirmacao) => void;
}

/**
 * Uma única janela de confirmação para o portal inteiro, no lugar do `window.confirm`
 * do navegador. A tela chama `confirmar(...)` (hook useConfirmacao), espera o "sim" e
 * só então fala com a API.
 */
export function ConfirmacaoProvider({ children }: { children: ReactNode }) {
  const [pendente, setPendente] = useState<Pendente | null>(null);
  const [texto, setTexto] = useState('');
  const [erroTexto, setErroTexto] = useState('');
  // Uma segunda pergunta enquanto a primeira está aberta recebe "não", em vez de sumir com ela.
  const aberta = useRef(false);

  const perguntar = useCallback<Perguntar>((opcoes) => {
    if (aberta.current) {
      return Promise.resolve({ confirmado: false, texto: '' });
    }

    aberta.current = true;
    setTexto('');
    setErroTexto('');

    return new Promise((resolver) => {
      setPendente({
        opcoes,
        responder: (resposta) => {
          aberta.current = false;
          setPendente(null);
          resolver(resposta);
        },
      });
    });
  }, []);

  function cancelar() {
    pendente?.responder({ confirmado: false, texto: '' });
  }

  function confirmar() {
    if (!pendente) return;

    const valor = texto.trim();

    if (pendente.opcoes.campoTexto?.obrigatorio && !valor) {
      setErroTexto(`Informe ${pendente.opcoes.campoTexto.rotulo.toLowerCase()}.`);
      return;
    }

    pendente.responder({ confirmado: true, texto: valor });
  }

  const opcoes = pendente?.opcoes;

  return (
    <ContextoDeConfirmacao.Provider value={perguntar}>
      {children}

      {opcoes && (
        <Modal aberto titulo={opcoes.titulo} aoFechar={cancelar} largura="max-w-md">
          <div className="flex items-start gap-3">
            <span
              className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${
                opcoes.perigo ? 'bg-perigo-claro text-perigo' : 'bg-brand-100 text-brand-dark'
              }`}
            >
              {opcoes.perigo ? <AlertTriangle size={20} /> : <HelpCircle size={20} />}
            </span>
            <div className="min-w-0 flex-1 text-sm leading-relaxed text-slate-700">{opcoes.mensagem}</div>
          </div>

          {opcoes.campoTexto && (
            <div className="mt-4">
              <label className="vc-rotulo" htmlFor="confirmacao-texto">
                {opcoes.campoTexto.rotulo}
                {opcoes.campoTexto.obrigatorio && <span className="ml-0.5 text-perigo">*</span>}
              </label>
              <textarea
                id="confirmacao-texto"
                className="vc-campo"
                rows={2}
                value={texto}
                onChange={(e) => {
                  setTexto(e.target.value);
                  setErroTexto('');
                }}
                placeholder={opcoes.campoTexto.placeholder}
                autoFocus
              />
              {erroTexto && <p className="mt-1 text-xs text-perigo">{erroTexto}</p>}
            </div>
          )}

          <div className="mt-6 flex justify-end gap-2">
            <button type="button" className="vc-botao-secundario" onClick={cancelar} autoFocus={!opcoes.campoTexto}>
              {opcoes.rotuloCancelar ?? 'Cancelar'}
            </button>
            <button
              type="button"
              className={opcoes.perigo ? 'vc-botao-perigo' : 'vc-botao-primario'}
              onClick={confirmar}
            >
              {opcoes.rotuloConfirmar ?? 'Confirmar'}
            </button>
          </div>
        </Modal>
      )}
    </ContextoDeConfirmacao.Provider>
  );
}
