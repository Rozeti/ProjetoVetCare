import { useEffect, useRef, useState } from 'react';
import { api } from '../services/api';
import { useAuth } from '../contexts/auth';
import type { Veterinario } from '../types';
import { Campo } from './ui';

interface Props {
  valor: string;
  aoMudar: (veterinarioId: string) => void;
  rotulo?: string;
  obrigatorio?: boolean;
  dica?: string;
}

/**
 * Escolha do profissional responsável por um registro clínico.
 *
 * Quem está logado como veterinário não vê o campo: a API assina o registro em nome dele e
 * recusa qualquer outro. Já o administrador e o apoio precisam dizer de quem é a
 * responsabilidade técnica, porque eles próprios não são profissionais habilitados.
 */
export function SeletorVeterinario({
  valor,
  aoMudar,
  rotulo = 'Veterinário responsável',
  obrigatorio,
  dica,
}: Props) {
  const { ehVeterinario } = useAuth();

  const [veterinarios, setVeterinarios] = useState<Veterinario[]>([]);
  const [falhouAoCarregar, setFalhouAoCarregar] = useState(false);

  // Guardar o callback numa referência mantém o efeito dependente apenas do perfil. Se ele
  // entrasse na lista de dependências, um callback recriado a cada render reiniciaria a
  // busca em laço.
  const aoMudarRef = useRef(aoMudar);

  useEffect(() => {
    aoMudarRef.current = aoMudar;
  });

  useEffect(() => {
    if (ehVeterinario) {
      return;
    }

    let ativo = true;

    async function carregar() {
      try {
        const { data } = await api.get<Veterinario[]>('/api/veterinarios');

        if (!ativo) return;

        setVeterinarios(data);

        // Com um único veterinário na clínica a escolha é óbvia e já vem resolvida.
        if (data.length === 1) {
          aoMudarRef.current(data[0].id);
        }
      } catch {
        if (ativo) setFalhouAoCarregar(true);
      }
    }

    carregar();

    return () => {
      ativo = false;
    };
  }, [ehVeterinario]);

  if (ehVeterinario) {
    return null;
  }

  if (falhouAoCarregar) {
    return (
      <Campo rotulo={rotulo} erro="Não foi possível carregar a lista de veterinários.">
        <input className="vc-campo" disabled value="" />
      </Campo>
    );
  }

  if (veterinarios.length === 0) {
    return (
      <Campo
        rotulo={rotulo}
        erro="Nenhum veterinário cadastrado. Cadastre o profissional em Usuários antes de continuar."
      >
        <input className="vc-campo" disabled value="" />
      </Campo>
    );
  }

  return (
    <Campo rotulo={rotulo} obrigatorio={obrigatorio} dica={dica}>
      <select className="vc-campo" value={valor} onChange={(e) => aoMudar(e.target.value)}>
        <option value="">Selecione o veterinário</option>
        {veterinarios.map((veterinario) => (
          <option key={veterinario.id} value={veterinario.id}>
            {veterinario.nome}
            {veterinario.crmv && ` — ${veterinario.crmv}`}
          </option>
        ))}
      </select>
    </Campo>
  );
}
