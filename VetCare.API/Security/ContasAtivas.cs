using Microsoft.Extensions.Caching.Memory;

namespace VetCare.API.Security
{
    /// <summary>
    /// Um JWT continua válido até expirar, mesmo que o Administrador desative a conta no
    /// meio do caminho (HU-002, CA-4). Para que a desativação valha na hora, cada requisição
    /// confere se a conta segue ativa — com um cache curto, para não ir ao banco a cada
    /// chamada — e a desativação apaga a entrada do cache na mesma operação.
    /// </summary>
    public sealed class ContasAtivas
    {
        private static readonly TimeSpan Validade = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache _cache;

        public ContasAtivas(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<bool> EstaAtiva(Guid usuarioId, Func<Task<bool>> consultar)
        {
            if (_cache.TryGetValue(Chave(usuarioId), out bool ativa))
            {
                return ativa;
            }

            ativa = await consultar();
            _cache.Set(Chave(usuarioId), ativa, Validade);

            return ativa;
        }

        /// <summary>Chamado quando a conta é ativada ou desativada, para a mudança valer imediatamente.</summary>
        public void Invalidar(Guid usuarioId) => _cache.Remove(Chave(usuarioId));

        private static string Chave(Guid usuarioId) => $"conta-ativa:{usuarioId:N}";
    }
}
