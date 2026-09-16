using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IProntuarioRepository
    {
        Task<Prontuario?> ObterPorId(Guid id);
        Task<Prontuario?> ObterPorPacienteId(Guid pacienteId);

        /// <summary>
        /// HU-011: garante que o paciente sempre tenha prontuário ao registrar o primeiro
        /// item clínico, evitando um passo manual de abertura.
        /// </summary>
        Task<Prontuario> ObterOuCriarPorPacienteId(Guid pacienteId);

        Task<List<AvaliacaoClinica>> ObterAvaliacoes(Guid prontuarioId);
        Task<List<AtendimentoFisioterapeutico>> ObterAtendimentos(Guid prontuarioId);

        /// <summary>Mídias das sessões ligadas a este prontuário, para a linha do tempo (HU-011, CA-4).</summary>
        Task<List<MidiaSessao>> ObterMidias(Guid prontuarioId);

        Task<List<ObservacaoInterna>> ObterObservacoesInternas(Guid prontuarioId);
        Task<List<DocumentoClinico>> ObterDocumentos(Guid prontuarioId);

        Task Adicionar(Prontuario prontuario);
        void Atualizar(Prontuario prontuario);
        Task SalvarAlteracoes();
    }
}
