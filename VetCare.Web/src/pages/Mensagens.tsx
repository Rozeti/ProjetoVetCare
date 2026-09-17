import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { MessageSquare, Plus, Send, X } from 'lucide-react';
import { api, mensagemDeErro } from '../services/api';
import { useAtualizacao } from '../contexts/atualizacoes';
import type { Conversa, Mensagem, Usuario } from '../types';
import { Alerta, Avatar, CabecalhoPagina, Card, Carregando, Modal, SemDados } from '../components/ui';
import { formatarHora, tempoRelativo } from '../utils/formato';

/** HU-014: troca de mensagens entre tutor e veterinário. */
export function Mensagens() {
  const [conversas, setConversas] = useState<Conversa[]>([]);
  const [contatos, setContatos] = useState<Usuario[]>([]);
  const [selecionado, setSelecionado] = useState<string | null>(null);
  const [mensagens, setMensagens] = useState<Mensagem[]>([]);
  const [texto, setTexto] = useState('');

  const [carregandoLista, setCarregandoLista] = useState(true);
  const [carregandoConversa, setCarregandoConversa] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState('');
  const [modalNovaConversa, setModalNovaConversa] = useState(false);

  const fimDaLista = useRef<HTMLDivElement>(null);

  // Toda ação que muda a lista de conversas incrementa esta versão, que é o que
  // dispara uma nova leitura.
  const [versaoDaLista, setVersaoDaLista] = useState(0);
  const atualizarLista = useCallback(() => setVersaoDaLista((atual) => atual + 1), []);

  // A conversa aberta tem a própria versão para que uma mensagem recebida a atualize
  // sem depender de o usuário trocar de contato.
  const [versaoDaConversa, setVersaoDaConversa] = useState(0);

  // HU-014: a mensagem chega à tela no momento em que o outro lado a envia.
  const aoChegarMensagem = useCallback(() => {
    atualizarLista();
    setVersaoDaConversa((atual) => atual + 1);
  }, [atualizarLista]);

  useAtualizacao(['mensagens'], aoChegarMensagem);

  useEffect(() => {
    let ativo = true;

    async function carregar() {
      try {
        const { data } = await api.get<Conversa[]>('/api/mensagens/conversas');
        if (ativo) setConversas(data);
      } catch (falha) {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar as conversas.'));
      } finally {
        if (ativo) setCarregandoLista(false);
      }
    }

    carregar();

    // O mural de atualizações já avisa quando chega mensagem; a leitura periódica
    // fica como rede de segurança para o caso de a conexão com ele cair.
    const intervalo = window.setInterval(carregar, 60_000);

    return () => {
      ativo = false;
      window.clearInterval(intervalo);
    };
  }, [versaoDaLista]);

  useEffect(() => {
    if (!selecionado) return;

    let ativo = true;

    async function carregar(usuarioId: string) {
      setCarregandoConversa(true);

      try {
        const { data } = await api.get<Mensagem[]>(`/api/mensagens/conversa/${usuarioId}`);

        if (ativo) {
          setMensagens(data);
          setErro('');

          // HU-014, CA-3: a leitura acima zera o contador de não lidas no servidor,
          // então a lista precisa ser relida para refletir isso.
          atualizarLista();
        }
      } catch (falha) {
        if (ativo) setErro(mensagemDeErro(falha, 'Não foi possível carregar a conversa.'));
      } finally {
        if (ativo) setCarregandoConversa(false);
      }
    }

    carregar(selecionado);

    return () => {
      ativo = false;
    };
  }, [selecionado, atualizarLista, versaoDaConversa]);

  useEffect(() => {
    fimDaLista.current?.scrollIntoView({ behavior: 'smooth' });
  }, [mensagens]);

  async function abrirNovaConversa() {
    try {
      const { data } = await api.get<Usuario[]>('/api/mensagens/contatos');
      setContatos(data);
      setModalNovaConversa(true);
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível carregar os contatos.'));
    }
  }

  async function enviar(evento: FormEvent) {
    evento.preventDefault();

    if (!selecionado || !texto.trim()) return;

    setEnviando(true);
    setErro('');

    try {
      const { data } = await api.post<Mensagem>('/api/mensagens', {
        destinatarioId: selecionado,
        conteudo: texto.trim(),
      });

      setMensagens((atual) => [...atual, data]);
      setTexto('');
      atualizarLista();
    } catch (falha) {
      setErro(mensagemDeErro(falha, 'Não foi possível enviar a mensagem.'));
    } finally {
      setEnviando(false);
    }
  }

  const conversaAtual = conversas.find((c) => c.usuarioId === selecionado);
  const nomeContato = conversaAtual?.nome ?? contatos.find((c) => c.id === selecionado)?.nome ?? '';

  return (
    <>
      <CabecalhoPagina
        titulo="Mensagens"
        descricao="Converse sobre o tratamento sem sair do sistema."
        acoes={
          <button type="button" className="vc-botao-primario" onClick={abrirNovaConversa}>
            <Plus size={16} />
            Nova conversa
          </button>
        }
      />

      {erro && (
        <div className="mb-4">
          <Alerta tipo="erro" aoFechar={() => setErro('')}>
            {erro}
          </Alerta>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-1">
          <div className="border-b border-slate-200 px-5 py-3">
            <h2 className="text-sm font-semibold text-slate-700">Conversas</h2>
          </div>

          {carregandoLista ? (
            <Carregando texto="Carregando..." />
          ) : conversas.length === 0 ? (
            <SemDados
              icone={<MessageSquare size={36} />}
              titulo="Nenhuma conversa ainda"
              descricao="Inicie uma conversa com a equipe clínica ou com um tutor."
            />
          ) : (
            <ul className="max-h-[32rem] divide-y divide-slate-100 overflow-y-auto">
              {conversas.map((conversa) => (
                <li key={conversa.usuarioId}>
                  <button
                    type="button"
                    onClick={() => setSelecionado(conversa.usuarioId)}
                    className={`flex w-full items-center gap-3 px-4 py-3 text-left transition ${
                      selecionado === conversa.usuarioId ? 'bg-brand-50' : 'hover:bg-slate-50'
                    }`}
                  >
                    <div className="relative">
                      <Avatar nome={conversa.nome} />
                      {/* HU-014, CA-2: indicador de presença. */}
                      {conversa.online && (
                        <span
                          className="absolute -bottom-0.5 -right-0.5 h-3 w-3 rounded-full border-2 border-white bg-sucesso"
                          title="Online"
                        />
                      )}
                    </div>

                    <div className="min-w-0 flex-1">
                      <div className="flex items-baseline justify-between gap-2">
                        <p className="truncate text-sm font-semibold text-slate-900">{conversa.nome}</p>
                        <span className="shrink-0 text-xs text-slate-400">
                          {tempoRelativo(conversa.dataUltimaMensagem)}
                        </span>
                      </div>
                      <p className="truncate text-xs text-slate-500">{conversa.ultimaMensagem}</p>
                    </div>

                    {conversa.naoLidas > 0 && (
                      <span className="shrink-0 rounded-full bg-brand px-2 py-0.5 text-xs font-bold text-white">
                        {conversa.naoLidas}
                      </span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card className="flex min-h-[32rem] flex-col lg:col-span-2">
          {!selecionado ? (
            <SemDados
              icone={<MessageSquare size={40} />}
              titulo="Selecione uma conversa"
              descricao="Escolha um contato na lista ao lado para ver o histórico de mensagens."
            />
          ) : (
            <>
              <div className="flex items-center gap-3 border-b border-slate-200 px-5 py-3">
                <Avatar nome={nomeContato} />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold text-slate-900">{nomeContato}</p>
                  {conversaAtual && (
                    <p className="text-xs text-slate-500">
                      {conversaAtual.perfil} · {conversaAtual.online ? 'Online agora' : 'Offline'}
                    </p>
                  )}
                </div>

                <button
                  type="button"
                  onClick={() => setSelecionado(null)}
                  className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 lg:hidden"
                  aria-label="Fechar conversa"
                >
                  <X size={18} />
                </button>
              </div>

              <div className="flex-1 space-y-3 overflow-y-auto px-5 py-4">
                {carregandoConversa ? (
                  <Carregando texto="Carregando mensagens..." />
                ) : mensagens.length === 0 ? (
                  <p className="py-10 text-center text-sm text-slate-500">
                    Nenhuma mensagem ainda. Escreva a primeira abaixo.
                  </p>
                ) : (
                  mensagens.map((mensagem) => (
                    <div
                      key={mensagem.id}
                      className={`flex ${mensagem.propria ? 'justify-end' : 'justify-start'}`}
                    >
                      <div
                        className={`max-w-[75%] rounded-2xl px-4 py-2.5 ${
                          mensagem.propria
                            ? 'rounded-br-sm bg-brand text-white'
                            : 'rounded-bl-sm bg-slate-100 text-slate-800'
                        }`}
                      >
                        {mensagem.nomePaciente && (
                          <p
                            className={`mb-1 text-xs font-semibold ${
                              mensagem.propria ? 'text-brand-100' : 'text-slate-500'
                            }`}
                          >
                            Sobre {mensagem.nomePaciente}
                          </p>
                        )}

                        <p className="whitespace-pre-wrap text-sm">{mensagem.conteudo}</p>

                        <p className={`mt-1 text-right text-[10px] ${mensagem.propria ? 'text-brand-100' : 'text-slate-400'}`}>
                          {formatarHora(mensagem.dataEnvio)}
                        </p>
                      </div>
                    </div>
                  ))
                )}
                <div ref={fimDaLista} />
              </div>

              <form onSubmit={enviar} className="flex items-end gap-2 border-t border-slate-200 p-4">
                <textarea
                  className="vc-campo resize-none"
                  rows={2}
                  placeholder="Escreva sua mensagem..."
                  value={texto}
                  onChange={(e) => setTexto(e.target.value)}
                  onKeyDown={(e) => {
                    // Enter envia; Shift+Enter quebra linha.
                    if (e.key === 'Enter' && !e.shiftKey) {
                      e.preventDefault();
                      enviar(e as unknown as FormEvent);
                    }
                  }}
                  disabled={enviando}
                />

                <button type="submit" className="vc-botao-primario shrink-0" disabled={enviando || !texto.trim()}>
                  <Send size={16} />
                  Enviar
                </button>
              </form>
            </>
          )}
        </Card>
      </div>

      <Modal aberto={modalNovaConversa} titulo="Nova conversa" aoFechar={() => setModalNovaConversa(false)}>
        {contatos.length === 0 ? (
          <p className="text-sm text-slate-500">Nenhum contato disponível no momento.</p>
        ) : (
          <ul className="max-h-96 divide-y divide-slate-100 overflow-y-auto">
            {contatos.map((contato) => (
              <li key={contato.id}>
                <button
                  type="button"
                  onClick={() => {
                    setSelecionado(contato.id);
                    setModalNovaConversa(false);
                  }}
                  className="flex w-full items-center gap-3 px-2 py-3 text-left transition hover:bg-slate-50"
                >
                  <Avatar nome={contato.nome} />
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold text-slate-900">{contato.nome}</p>
                    <p className="truncate text-xs text-slate-500">{contato.perfil}</p>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </>
  );
}
