import { useCallback, useEffect, useState } from 'react';
import { mensagemDeErro } from '../services/api';

interface Carregamento<T> {
  dados: T | null;
  carregando: boolean;
  erro: string;
  setErro: (mensagem: string) => void;
  /** Refaz a busca depois de uma ação que alterou os dados no servidor. */
  recarregar: () => void;
}

/**
 * Busca um recurso da API e devolve o trio carregando/erro/dados que as telas usam.
 *
 * A requisição roda dentro do próprio efeito e o resultado é descartado quando o
 * componente sai de cena ou quando uma busca mais nova começa. Isso evita atualizar o
 * estado de um componente desmontado e impede que uma resposta lenta sobrescreva outra
 * mais recente — o que acontecia ao trocar de filtro ou de página rapidamente.
 *
 * `buscar` precisa vir de um `useCallback`: é ele que define quando a tela recarrega.
 *
 * `atrasoMs` adia a requisição para telas com campo de busca, de modo que digitar não
 * dispare uma chamada por tecla: cada tecla cancela o disparo anterior.
 */
export function useCarregamento<T>(
  buscar: () => Promise<T>,
  mensagemDeFalha: string,
  atrasoMs = 0,
): Carregamento<T> {
  const [dados, setDados] = useState<T | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');
  const [versao, setVersao] = useState(0);

  useEffect(() => {
    let ativo = true;

    const temporizador = window.setTimeout(async () => {
      try {
        const resultado = await buscar();

        if (ativo) {
          setDados(resultado);
          setErro('');
        }
      } catch (falha) {
        if (ativo) {
          setErro(mensagemDeErro(falha, mensagemDeFalha));
        }
      } finally {
        if (ativo) {
          setCarregando(false);
        }
      }
    }, atrasoMs);

    return () => {
      ativo = false;
      window.clearTimeout(temporizador);
    };
  }, [buscar, mensagemDeFalha, atrasoMs, versao]);

  const recarregar = useCallback(() => setVersao((atual) => atual + 1), []);

  return { dados, carregando, erro, setErro, recarregar };
}
