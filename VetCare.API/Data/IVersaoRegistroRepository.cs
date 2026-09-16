using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IVersaoRegistroRepository
    {
        /// <summary>RN-004: arquiva o estado anterior antes de aplicar a edição.</summary>
        Task Arquivar(string tipoRegistro, Guid registroId, object estadoAnterior, Guid alteradoPorId);

        Task<List<VersaoRegistroClinico>> ObterHistorico(string tipoRegistro, Guid registroId);
        Task SalvarAlteracoes();
    }
}
