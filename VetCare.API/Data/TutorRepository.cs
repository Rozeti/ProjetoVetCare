using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class TutorRepository : ITutorRepository
    {
        private readonly AppDbContext _context;

        public TutorRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(Tutor tutor) => await _context.Tutores.AddAsync(tutor);

        public async Task<Tutor?> ObterPorId(Guid id)
        {
            return await _context.Tutores
                .Include(t => t.Usuario)
                .Include(t => t.Pets)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Tutor?> ObterPorUsuarioId(Guid usuarioId)
        {
            return await _context.Tutores
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.UsuarioId == usuarioId);
        }

        public async Task<PaginaDe<Tutor>> Listar(Guid clinicaId, string? busca, ParametrosPagina parametros)
        {
            return await Filtrar(clinicaId, busca).OrderBy(t => t.Usuario!.Nome).Paginar(parametros);
        }

        public async Task<List<Tutor>> ListarTodos(Guid clinicaId)
        {
            return await Filtrar(clinicaId, null).OrderBy(t => t.Usuario!.Nome).ToListAsync();
        }

        private IQueryable<Tutor> Filtrar(Guid clinicaId, string? busca)
        {
            var consulta = _context.Tutores
                .AsNoTracking()
                .Include(t => t.Usuario)
                .Include(t => t.Pets)
                .Where(t => t.Usuario!.ClinicaId == clinicaId);

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLowerInvariant();
                consulta = consulta.Where(t =>
                    t.Usuario!.Nome.ToLower().Contains(termo) ||
                    t.Usuario!.Email.ToLower().Contains(termo) ||
                    t.Telefone.Contains(termo) ||
                    t.Cpf.Contains(termo));
            }

            return consulta;
        }

        public void Atualizar(Tutor tutor) => _context.Tutores.Update(tutor);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
