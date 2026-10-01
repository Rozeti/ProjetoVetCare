using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface ITratamentoRepository
    {
        Task Adicionar(Tratamento tratamento);
        Task<Tratamento?> ObterPorId(Guid id);
        Task<Tratamento?> ObterPorIdComRelacionamentos(Guid id);
        Task<List<Tratamento>> ObterPorPaciente(Guid pacienteId);
        Task<List<Tratamento>> ObterPorVeterinario(Guid veterinarioId);
        void Atualizar(Tratamento tratamento);
        Task SalvarAlteracoes();
    }
}
