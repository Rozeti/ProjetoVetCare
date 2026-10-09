import { Link } from 'react-router-dom';
import { Info } from 'lucide-react';
import { Modal } from '../../components/ui';
import { useAuth } from '../../contexts/auth';
import type { RelatorioProdutividade } from '../../types';
import { formatarData } from '../../utils/formato';

interface Props {
  /** Relatório que acabou de ser gerado sem atendimentos; `null` mantém a janela fechada. */
  relatorio: RelatorioProdutividade | null;
  aoFechar: () => void;
}

interface Passo {
  titulo: string;
  detalhe: string;
  link?: { rotulo: string; caminho: string };
}

/**
 * HU-017: explica o que precisa existir no sistema para o relatório trazer dados.
 *
 * O relatório só conta registros clínicos já feitos: atendimentos (HU-008), que alimentam
 * os quadros por veterinário e o ranking de técnicas, e avaliações (HU-007). Os dois são
 * contados pela data em que foram registrados, dentro da clínica do usuário — e, para o
 * veterinário, apenas os próprios (RN-008).
 */
export function ModalComoGerarRelatorio({ relatorio, aoFechar }: Props) {
  const { ehVeterinario } = useAuth();

  if (!relatorio) return null;

  const periodo = `${formatarData(relatorio.inicio)} a ${formatarData(relatorio.fim)}`;
  const agenda = ehVeterinario
    ? { rotulo: 'Ir para a Agenda', caminho: '/agenda' }
    : { rotulo: 'Ir para a Agenda geral', caminho: '/agenda-geral' };

  const passos: Passo[] = [
    {
      titulo: 'Abra um tratamento para o paciente',
      detalhe: ehVeterinario
        ? 'Em Pacientes, abra o prontuário de um paciente sob a sua responsabilidade e clique em "Tratamento".'
        : 'Em Pacientes, abra o prontuário do paciente e clique em "Tratamento".',
      link: { rotulo: 'Ir para Pacientes', caminho: '/pacientes' },
    },
    {
      titulo: 'Registre a avaliação clínica',
      detalhe:
        'No mesmo prontuário, use "Registrar avaliação" e preencha os cinco campos obrigatórios (queixa principal, anamnese, exame físico, hipótese diagnóstica e plano terapêutico). Ela entra no total de avaliações do relatório.',
    },
    {
      titulo: 'Agende uma sessão do tratamento',
      detalhe: ehVeterinario
        ? 'Na Agenda, clique em "Agendar sessão" e escolha o paciente e o tratamento ativo.'
        : 'Na Agenda geral, clique em "Agendar sessão" e escolha o paciente e o tratamento ativo. A sessão fica com o veterinário responsável pelo paciente.',
      link: agenda,
    },
    {
      titulo: 'Registre o atendimento da sessão',
      detalhe: ehVeterinario
        ? 'Na sua Agenda, clique no ícone "Registrar atendimento" da sessão e informe a escala de dor (0 a 10), as técnicas aplicadas separadas por vírgula e a evolução clínica. Sessões canceladas não aceitam atendimento.'
        : 'O atendimento é registrado pelo veterinário responsável, na Agenda dele, pelo ícone "Registrar atendimento" da sessão — com escala de dor (0 a 10), técnicas aplicadas separadas por vírgula e evolução clínica. Sessões canceladas não aceitam atendimento.',
    },
    {
      titulo: 'Gere o relatório novamente',
      detalhe:
        'Volte a esta tela e escolha um período que inclua a data em que o atendimento foi registrado (vale a data do registro, não a data agendada da sessão).',
    },
  ];

  return (
    <Modal
      aberto
      titulo="Ainda não há dados para o relatório"
      descricao={`Período de ${periodo}`}
      aoFechar={aoFechar}
      largura="max-w-2xl"
    >
      <div className="mb-5 flex items-start gap-3 rounded-xl bg-info-claro p-4 text-sm text-slate-700">
        <Info size={20} className="mt-0.5 shrink-0 text-info" />
        <p>
          {relatorio.totalAvaliacoes > 0
            ? `Há ${relatorio.totalAvaliacoes} avaliação(ões) no período, mas nenhum atendimento. Os atendimentos por veterinário, a média da escala de dor e o ranking de técnicas dependem de atendimentos registrados nas sessões.`
            : 'O relatório é montado a partir das avaliações e dos atendimentos registrados no período, e nenhum foi encontrado.'}{' '}
          {ehVeterinario
            ? 'Como veterinário(a), você vê apenas os seus próprios registros.'
            : 'Como administrador, você vê os registros de todos os veterinários da clínica.'}
        </p>
      </div>

      <p className="mb-3 text-sm font-medium text-slate-900">Para gerar o relatório, siga estes passos:</p>

      <ol className="space-y-4">
        {passos.map((passo, indice) => (
          <li key={passo.titulo} className="flex gap-3">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-bold text-brand-dark">
              {indice + 1}
            </span>

            <div className="min-w-0 flex-1">
              <p className="font-medium text-slate-900">{passo.titulo}</p>
              <p className="mt-0.5 text-sm text-slate-600">{passo.detalhe}</p>
              {passo.link && (
                <Link to={passo.link.caminho} className="mt-1 inline-block text-sm font-medium text-brand hover:underline">
                  {passo.link.rotulo}
                </Link>
              )}
            </div>
          </li>
        ))}
      </ol>

      <div className="mt-6 flex justify-end">
        <button type="button" className="vc-botao-primario" onClick={aoFechar}>
          Entendi
        </button>
      </div>
    </Modal>
  );
}
