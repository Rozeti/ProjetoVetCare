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

        public async Task<TokenRedefinicaoSenha?> ObterAbertoDoUsuario(Guid usuarioId)
        {
            return await _context.TokensRedefinicaoSenha
                .Include(t => t.Usuario)
                .Where(t => t.UsuarioId == usuarioId && t.UtilizadoEm == null)
                .OrderByDescending(t => t.DataCriacao)
                .FirstOrDefaultAsync();
        }

        public async Task InvalidarAnteriores(Guid usuarioId)
        {
            // Um usuário tem no máximo um punhado de pedidos em aberto; carregar e marcar
            // um a um dispensa comandos específicos do provedor e funciona em qualquer banco.
            var abertos = await _context.TokensRedefinicaoSenha
                .Where(t => t.UsuarioId == usuarioId && t.UtilizadoEm == null)
                .ToListAsync();

            foreach (var token in abertos)
            {
                token.UtilizadoEm = DateTime.UtcNow;
            }
        }

        public void Atualizar(TokenRedefinicaoSenha token) => _context.TokensRedefinicaoSenha.Update(token);

        public async Task SalvarAlteracoes() => await _context.SaveChangesAsync();
    }
}
