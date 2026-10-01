using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class DispositivoRepository : IDispositivoRepository
    {
        private readonly AppDbContext _context;

        public DispositivoRepository(AppDbContext context) => _context = context;

        public async Task<DispositivoDoUsuario?> ObterPorToken(string tokenPush) =>
            await _context.DispositivosDoUsuario.FirstOrDefaultAsync(d => d.TokenPush == tokenPush);

        public async Task<List<DispositivoDoUsuario>> ObterAtivosDoUsuario(Guid usuarioId)
        {
            return await _context.DispositivosDoUsuario
                .AsNoTracking()
                .Where(d => d.UsuarioId == usuarioId && d.Ativo)
                .OrderByDescending(d => d.UltimoUsoEm)
                .ToListAsync();
        }

        public async Task<List<DispositivoDoUsuario>> ObterAtivosDosUsuarios(IEnumerable<Guid> usuariosIds)
        {
            var ids = usuariosIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new List<DispositivoDoUsuario>();
            }

            return await _context.DispositivosDoUsuario
                .Where(d => d.Ativo && ids.Contains(d.UsuarioId))
                .ToListAsync();
        }

        public async Task Adicionar(DispositivoDoUsuario dispositivo) =>
            await _context.DispositivosDoUsuario.AddAsync(dispositivo);

        public void Atualizar(DispositivoDoUsuario dispositivo) =>
            _context.DispositivosDoUsuario.Update(dispositivo);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
