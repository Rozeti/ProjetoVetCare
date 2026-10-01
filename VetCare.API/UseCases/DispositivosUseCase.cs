using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services.Push;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Aparelhos em que o usuário recebe notificações no celular (HU-015). O aplicativo
    /// registra o token de push ao entrar e o devolve ao sair; o resto é automático.
    /// </summary>
    public class DispositivosUseCase
    {
        private static readonly string[] PlataformasConhecidas = { "android", "ios" };

        private readonly IDispositivoRepository _dispositivos;
        private readonly UsuarioAtual _usuarioAtual;

        public DispositivosUseCase(IDispositivoRepository dispositivos, UsuarioAtual usuarioAtual)
        {
            _dispositivos = dispositivos;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<DispositivoDTO>> Registrar(RegistrarDispositivoDTO dto)
        {
            var token = dto.TokenPush.Trim();

            if (!ServicoDePushExpo.TokenTemFormatoValido(token))
            {
                return Resultado<DispositivoDTO>.Invalido("Token de notificação inválido.");
            }

            var plataforma = dto.Plataforma.Trim().ToLowerInvariant();

            if (!PlataformasConhecidas.Contains(plataforma))
            {
                return Resultado<DispositivoDTO>.Invalido("Plataforma inválida. Use \"android\" ou \"ios\".");
            }

            var nomeDoAparelho = (dto.NomeDoAparelho ?? string.Empty).Trim();
            var dispositivo = await _dispositivos.ObterPorToken(token);

            if (dispositivo == null)
            {
                dispositivo = new DispositivoDoUsuario
                {
                    UsuarioId = _usuarioAtual.Id,
                    TokenPush = token,
                    Plataforma = plataforma,
                    NomeDoAparelho = nomeDoAparelho
                };

                await _dispositivos.Adicionar(dispositivo);
            }
            else
            {
                // O mesmo celular pode trocar de dono (outro tutor entrou nele): o token
                // passa a pertencer a quem está autenticado agora.
                dispositivo.UsuarioId = _usuarioAtual.Id;
                dispositivo.Plataforma = plataforma;
                dispositivo.NomeDoAparelho = nomeDoAparelho;
                dispositivo.UltimoUsoEm = DateTime.UtcNow;
                dispositivo.Ativo = true;

                _dispositivos.Atualizar(dispositivo);
            }

            await _dispositivos.SalvarAlteracoes();

            return Resultado<DispositivoDTO>.Ok(MapearParaDTO(dispositivo), "Aparelho registrado para receber notificações.");
        }

        /// <summary>Ao sair da conta, o aparelho deixa de receber avisos daquele usuário.</summary>
        public async Task<Resultado> Remover(string tokenPush)
        {
            var dispositivo = await _dispositivos.ObterPorToken((tokenPush ?? string.Empty).Trim());

            // Se o aparelho já é de outro usuário, nada a fazer: a saída é idempotente.
            if (dispositivo == null || dispositivo.UsuarioId != _usuarioAtual.Id || !dispositivo.Ativo)
            {
                return Resultado.Ok("Este aparelho já não recebia notificações.");
            }

            dispositivo.Ativo = false;
            _dispositivos.Atualizar(dispositivo);
            await _dispositivos.SalvarAlteracoes();

            return Resultado.Ok("Este aparelho não receberá mais notificações.");
        }

        public async Task<Resultado<List<DispositivoDTO>>> ListarMeus()
        {
            var dispositivos = await _dispositivos.ObterAtivosDoUsuario(_usuarioAtual.Id);
            return Resultado<List<DispositivoDTO>>.Ok(dispositivos.Select(MapearParaDTO).ToList());
        }

        private static DispositivoDTO MapearParaDTO(DispositivoDoUsuario dispositivo) => new()
        {
            Id = dispositivo.Id,
            Plataforma = dispositivo.Plataforma,
            NomeDoAparelho = dispositivo.NomeDoAparelho,
            RegistradoEm = dispositivo.RegistradoEm,
            UltimoUsoEm = dispositivo.UltimoUsoEm
        };
    }
}
