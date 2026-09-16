using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public interface IAuditoriaRepository
    {
        Task Registrar(RegistroAuditoria registro);

        Task<PaginaDe<RegistroAuditoria>> Listar(
            Guid clinicaId,
            Guid? usuarioId,
            string? acao,
            string? entidade,
            DateTime? inicio,
            DateTime? fim,
            ParametrosPagina parametros);
    }
}
