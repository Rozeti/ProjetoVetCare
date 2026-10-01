using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IApoioRepository
    {
        Task Adicionar(ApoioAdministrativo apoio);
        Task<ApoioAdministrativo?> ObterPorUsuarioId(Guid usuarioId);
        void Atualizar(ApoioAdministrativo apoio);
        Task SalvarAlteracoes();
    }
}
