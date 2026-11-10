import { useState, type ReactNode } from 'react';
import { FlaskConical, Pill } from 'lucide-react';
import { Modal } from '../../components/ui';
import { FormularioDeReceita } from './Receituario';
import { FormularioDePedido } from './PedidosExame';
import type { SessaoDeOrigem, TipoDeEmissao } from '../../utils/vinculoClinico';

interface Props {
  /** Sessão de origem; null mantém o modal fechado. */
  sessao: SessaoDeOrigem | null;
  aoFechar: () => void;
  /** Chamado depois da emissão: a tela leva o usuário à aba do documento escolhido. */
  aoSalvar: (tipo: TipoDeEmissao, mensagem: string) => void;
}

/**
 * Documentos clínicos emitidos a partir do registro de uma sessão: o veterinário escolhe entre a
 * receita de medicamento e o pedido de exame, preenche o formulário escolhido e é levado à aba
 * correspondente do prontuário. Os dois ficam ligados à sessão e ao tratamento dela.
 */
export function ModalReceitaOuExame({ sessao, ...props }: Props) {
  // Cada abertura monta o formulário do zero, sem efeito para limpá-lo.
  if (!sessao) return null;

  return <Conteudo key={sessao.id} sessao={sessao} {...props} />;
}

function Conteudo({ sessao, aoFechar, aoSalvar }: Omit<Props, 'sessao'> & { sessao: SessaoDeOrigem }) {
  const [tipo, setTipo] = useState<TipoDeEmissao | null>(null);

  // Os dois formulários ficam montados: trocar de opção não apaga o que já foi digitado.
  const origem = { atendimentoId: sessao.id };
  const sessoes = [sessao];

  return (
    <Modal
      aberto
      titulo="Receita ou pedido de exame"
      descricao={`${sessao.nomePaciente} · ${sessao.nomeTratamento}`}
      aoFechar={aoFechar}
      largura="max-w-3xl"
    >
      <fieldset className="mb-5">
        <legend className="mb-2 text-sm font-semibold text-slate-700">O que você vai emitir?</legend>

        <div className="grid gap-3 sm:grid-cols-2">
          <Opcao
            valor="receita"
            selecionado={tipo === 'receita'}
            aoEscolher={setTipo}
            icone={<Pill size={20} />}
            titulo="Receita de medicamento"
            descricao="Medicamentos, dosagem, frequência e validade. Vai para a aba Medicação."
          />
          <Opcao
            valor="exame"
            selecionado={tipo === 'exame'}
            aoEscolher={setTipo}
            icone={<FlaskConical size={20} />}
            titulo="Pedido de exame"
            descricao="Exames solicitados e justificativa clínica. Vai para a aba Exames."
          />
        </div>
      </fieldset>

      {!tipo && (
        <p className="rounded-xl bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
          Escolha uma opção para abrir o formulário. O documento fica ligado a esta sessão e ao tratamento{' '}
          {sessao.nomeTratamento}.
        </p>
      )}

      <div hidden={tipo !== 'receita'}>
        <FormularioDeReceita
          pacienteId={sessao.pacienteId}
          avaliacoes={[]}
          sessoes={sessoes}
          tratamentos={[]}
          origem={origem}
          aoCancelar={aoFechar}
          aoSalvar={(mensagem) => aoSalvar('receita', mensagem)}
        />
      </div>

      <div hidden={tipo !== 'exame'}>
        <FormularioDePedido
          avaliacoes={[]}
          sessoes={sessoes}
          tratamentos={[]}
          origem={origem}
          aoCancelar={aoFechar}
          aoSalvar={(mensagem) => aoSalvar('exame', mensagem)}
        />
      </div>
    </Modal>
  );
}

function Opcao({
  valor,
  selecionado,
  aoEscolher,
  icone,
  titulo,
  descricao,
}: {
  valor: TipoDeEmissao;
  selecionado: boolean;
  aoEscolher: (valor: TipoDeEmissao) => void;
  icone: ReactNode;
  titulo: string;
  descricao: string;
}) {
  return (
    <label
      className={`flex cursor-pointer items-start gap-3 rounded-xl border p-4 transition has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-brand/40 ${
        selecionado ? 'border-brand bg-brand-50' : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50'
      }`}
    >
      <input
        type="radio"
        name="tipo-de-emissao"
        value={valor}
        checked={selecionado}
        onChange={() => aoEscolher(valor)}
        className="sr-only"
      />
      <span className={`rounded-lg p-2 ${selecionado ? 'bg-brand text-white' : 'bg-slate-100 text-slate-500'}`}>
        {icone}
      </span>
      <span>
        <span className="block font-semibold text-slate-900">{titulo}</span>
        <span className="mt-0.5 block text-xs text-slate-500">{descricao}</span>
      </span>
    </label>
  );
}
