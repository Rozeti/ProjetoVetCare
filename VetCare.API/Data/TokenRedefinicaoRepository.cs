using Microsoft.EntityFrameworkCore;
using VetCare.API.Models;

namespace VetCare.API.Data
{
    public class TokenRedefinicaoRepository : ITokenRedefinicaoRepository
    {
        private readonly AppDbContext _context;

        public TokenRedefinicaoRepository(AppDbContext context) => _context = context;

        public async Task Adicionar(TokenRedefinicaoSenha token) =>
            await _context.TokensRedefinicaoSenha.AddAsync(token);

        public async Task<TokenRedefinicaoSenha?> ObterPorHash(string tokenHash)
        {
            return await _context.TokensRedefinicaoSenha
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public async Task InvalidarAnteriores(Guid usuarioId)
        {
            await _context.TokensRedefinicaoSenha
                .Where(t => t.UsuarioId == usuarioId && t.UtilizadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UtilizadoEm, DateTime.UtcNow));
        }

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
