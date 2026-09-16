using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IClinicaRepository
    {
        Task<Clinica?> ObterPorId(Guid id);
        Task<Clinica?> ObterPrimeira();
        void Atualizar(Clinica clinica);
        Task SalvarAlteracoes();
    }
}
