using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class AuditoriaRepository : IAuditoriaRepository
    {
        private readonly AppDbContext _context;

        public AuditoriaRepository(AppDbContext context) => _context = context;

        public async Task Registrar(RegistroAuditoria registro)
        {
            await _context.RegistrosAuditoria.AddAsync(registro);
            await _context.SaveChangesAsync();
        }

        public async Task<PaginaDe<RegistroAuditoria>> Listar(
            Guid clinicaId,
            Guid? usuarioId,
            string? acao,
            string? entidade,
            DateTime? inicio,
            DateTime? fim,
            ParametrosPagina parametros)
        {
            var consulta = _context.RegistrosAuditoria
                .AsNoTracking()
                .Where(r => r.ClinicaId == clinicaId);

            if (usuarioId.HasValue)
            {
                consulta = consulta.Where(r => r.UsuarioId == usuarioId.Value);
            }

            if (!string.IsNullOrWhiteSpace(acao))
            {
                consulta = consulta.Where(r => r.Acao == acao);
            }

            if (!string.IsNullOrWhiteSpace(entidade))
            {
                consulta = consulta.Where(r => r.Entidade == entidade);
            }

            if (inicio.HasValue)
            {
                var inicioUtc = DateTime.SpecifyKind(inicio.Value.Date, DateTimeKind.Utc);
                consulta = consulta.Where(r => r.DataHora >= inicioUtc);
            }

            if (fim.HasValue)
            {
                var fimUtc = DateTime.SpecifyKind(fim.Value.Date.AddDays(1), DateTimeKind.Utc);
                consulta = consulta.Where(r => r.DataHora < fimUtc);
            }

            return await consulta.OrderByDescending(r => r.DataHora).Paginar(parametros);
        }
    }
}
