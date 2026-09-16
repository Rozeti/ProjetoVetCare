using Microsoft.EntityFrameworkCore;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly AppDbContext _context;

        public UsuarioRepository(AppDbContext context) => _context = context;

        public async Task<Usuario?> ObterPorEmail(string email)
        {
            var normalizado = email.Trim().ToLowerInvariant();
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizado);
        }

        public async Task<Usuario?> ObterPorId(Guid id) =>
            await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

        public async Task Adicionar(Usuario usuario) => await _context.Usuarios.AddAsync(usuario);

        public async Task<bool> EmailExiste(string email, Guid? ignorarUsuarioId = null)
        {
            var normalizado = email.Trim().ToLowerInvariant();

            return await _context.Usuarios.AnyAsync(u =>
                u.Email.ToLower() == normalizado &&
                (ignorarUsuarioId == null || u.Id != ignorarUsuarioId));
        }

        public async Task<PaginaDe<Usuario>> Listar(
            Guid clinicaId,
            string? perfil,
            string? busca,
            bool? ativo,
            ParametrosPagina parametros)
        {
            return await Filtrar(clinicaId, perfil, busca, ativo).OrderBy(u => u.Nome).Paginar(parametros);
        }

        public async Task<List<Usuario>> ListarTodos(Guid clinicaId, string? perfil, bool? ativo)
        {
            return await Filtrar(clinicaId, perfil, null, ativo).OrderBy(u => u.Nome).ToListAsync();
        }

        private IQueryable<Usuario> Filtrar(Guid clinicaId, string? perfil, string? busca, bool? ativo)
        {
            var consulta = _context.Usuarios.AsNoTracking().Where(u => u.ClinicaId == clinicaId);

            if (!string.IsNullOrWhiteSpace(perfil))
            {
                consulta = consulta.Where(u => u.Perfil == perfil);
            }

            if (ativo.HasValue)
            {
                consulta = consulta.Where(u => u.Ativo == ativo.Value);
            }

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLowerInvariant();
                consulta = consulta.Where(u =>
                    u.Nome.ToLower().Contains(termo) || u.Email.ToLower().Contains(termo));
            }

            return consulta;
        }

        public void Atualizar(Usuario usuario) => _context.Usuarios.Update(usuario);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
