using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAlergiaRepository
    {
        Task Adicionar(AlergiaCondicao alergia);
        Task<AlergiaCondicao?> ObterPorId(Guid id);
        Task<List<AlergiaCondicao>> ObterPorPaciente(Guid pacienteId, bool apenasAtivas);
        void Atualizar(AlergiaCondicao alergia);
        Task SalvarAlteracoes();
    }
}
