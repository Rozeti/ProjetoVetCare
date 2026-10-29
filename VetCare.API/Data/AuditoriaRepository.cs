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
            IReadOnlyCollection<string>? acoes,
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

            if (acoes != null)
            {
                var lista = acoes.ToList();
                consulta = consulta.Where(r => lista.Contains(r.Acao));
            }

            if (!string.IsNullOrWhiteSpace(entidade))
            {
                consulta = consulta.Where(r => r.Entidade == entidade);
            }

            // O filtro é em dias da clínica: uma ação às 22h de Brasília pertence àquele dia, e
            // não ao seguinte, como aconteceria cortando o dia à meia-noite UTC.
            var relogio = RelogioDaClinica.Padrao;

            if (inicio.HasValue)
            {
                var inicioUtc = relogio.ParaUtc(relogio.DiaDaClinica(inicio.Value));
                consulta = consulta.Where(r => r.DataHora >= inicioUtc);
            }

            if (fim.HasValue)
            {
                var fimUtc = relogio.ParaUtc(relogio.DiaDaClinica(fim.Value).AddDays(1));
                consulta = consulta.Where(r => r.DataHora < fimUtc);
            }

            return await consulta.OrderByDescending(r => r.DataHora).Paginar(parametros);
        }
    }
}
