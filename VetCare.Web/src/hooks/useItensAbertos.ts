import { useState } from 'react';

/** Itens abertos de uma lista: o item em foco começa aberto e o clique alterna a partir daí. */
export function useItensAbertos(emFoco?: string) {
  const [alternados, setAlternados] = useState<Set<string>>(new Set());

  return {
    aberto: (id: string) => (id === emFoco) !== alternados.has(id),
    alternar: (id: string) =>
      setAlternados((atual) => {
        const novo = new Set(atual);
        if (novo.has(id)) novo.delete(id);
        else novo.add(id);
        return novo;
      }),
  };
}
