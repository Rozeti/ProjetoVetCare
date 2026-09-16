using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IDocumentoRepository
    {
        Task Adicionar(DocumentoClinico documento);
        Task<DocumentoClinico?> ObterPorId(Guid id);
        Task<List<DocumentoClinico>> ObterPorProntuario(Guid prontuarioId);
        Task SalvarAlteracoes();
    }
}
