using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IMidiaRepository
    {
        Task Adicionar(MidiaSessao midia);
        Task<MidiaSessao?> ObterPorId(Guid id);
        Task<List<MidiaSessao>> ObterPorSessao(Guid sessaoId);
        void Remover(MidiaSessao midia);
        Task SalvarAlteracoes();
    }
}
