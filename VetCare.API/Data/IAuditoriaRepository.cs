using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAuditoriaRepository
    {
        Task Registrar(RegistroAuditoria registro);

        /// <param name="acoes">Ações aceitas; nulo não filtra por ação.</param>
        Task<PaginaDe<RegistroAuditoria>> Listar(
            Guid clinicaId,
            Guid? usuarioId,
            IReadOnlyCollection<string>? acoes,
            string? entidade,
            DateTime? inicio,
            DateTime? fim,
            ParametrosPagina parametros);
    }
}
