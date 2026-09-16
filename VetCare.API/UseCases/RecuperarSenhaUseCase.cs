using System.Security.Cryptography;
using System.Text;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Redefinição de senha solicitada pelo próprio usuário, sem depender do
    /// Administrador. O token vale por tempo limitado e só pode ser usado uma vez.
    /// </summary>
    public class RecuperarSenhaUseCase
    {
        private static readonly TimeSpan ValidadeDoToken = TimeSpan.FromMinutes(30);

        private const string MensagemNeutra =
            "Se houver uma conta ativa com este e-mail, enviaremos as instruções de redefinição.";

        private readonly IUsuarioRepository _usuarios;
        private readonly ITokenRedefinicaoRepository _tokens;
        private readonly Security.PasswordHasher _hasher;
        private readonly AuditoriaService _auditoria;
        private readonly IWebHostEnvironment _ambiente;
        private readonly ILogger<RecuperarSenhaUseCase> _logger;

        public RecuperarSenhaUseCase(
            IUsuarioRepository usuarios,
            ITokenRedefinicaoRepository tokens,
            Security.PasswordHasher hasher,
            AuditoriaService auditoria,
            IWebHostEnvironment ambiente,
            ILogger<RecuperarSenhaUseCase> logger)
        {
            _usuarios = usuarios;
            _tokens = tokens;
            _hasher = hasher;
            _auditoria = auditoria;
            _ambiente = ambiente;
            _logger = logger;
        }

        /// <summary>
        /// A resposta é sempre a mesma, exista a conta ou não: revelar quais e-mails
        /// estão cadastrados permitiria mapear os usuários do sistema.
        /// </summary>
        public async Task<Resultado<RespostaRecuperacaoDTO>> Solicitar(SolicitarRecuperacaoDTO dto)
        {
            var resposta = new RespostaRecuperacaoDTO { Mensagem = MensagemNeutra };
            var usuario = await _usuarios.ObterPorEmail(dto.Email);

            if (usuario == null || !usuario.Ativo)
            {
                return Resultado<RespostaRecuperacaoDTO>.Ok(resposta);
            }

            var token = GerarToken();

            await _tokens.InvalidarAnteriores(usuario.Id);

            await _tokens.Adicionar(new TokenRedefinicaoSenha
            {
                UsuarioId = usuario.Id,
                TokenHash = CalcularHash(token),
                ExpiraEm = DateTime.UtcNow.Add(ValidadeDoToken)
            });

            await _tokens.SalvarAlteracoes();

            // O envio por SMTP previsto no DAS ainda não está conectado. Enquanto isso,
            // o token fica no log do servidor e, em desenvolvimento, na própria resposta.
            _logger.LogInformation(
                "Token de redefinição gerado para {Email}. Validade: {Minutos} minutos.",
                usuario.Email, ValidadeDoToken.TotalMinutes);

            if (_ambiente.IsDevelopment())
            {
                resposta.TokenDesenvolvimento = token;
            }

            return Resultado<RespostaRecuperacaoDTO>.Ok(resposta);
        }

        public async Task<Resultado> Redefinir(RedefinirComTokenDTO dto)
        {
            var registro = await _tokens.ObterPorHash(CalcularHash(dto.Token));

            if (registro == null || !registro.Valido || registro.Usuario == null)
            {
                return Resultado.Invalido("Link de redefinição inválido ou expirado. Solicite um novo.");
            }

            if (!registro.Usuario.Ativo)
            {
                return Resultado.Invalido("Esta conta está inativa. Procure o administrador da clínica.");
            }

            registro.Usuario.SenhaHash = _hasher.Gerar(dto.NovaSenha);

            // RN-006: a redefinição libera a conta de um bloqueio por tentativas.
            registro.Usuario.TentativasFalhas = 0;
            registro.Usuario.BloqueadoAte = null;

            registro.UtilizadoEm = DateTime.UtcNow;

            _usuarios.Atualizar(registro.Usuario);
            await _tokens.SalvarAlteracoes();

            await _auditoria.RegistrarDe(
                registro.Usuario,
                AuditoriaService.Acoes.RedefinicaoSenha,
                "Usuario",
                registro.Usuario.Id,
                "Senha redefinida pelo próprio usuário");

            return Resultado.Ok("Senha redefinida com sucesso. Use a nova senha para entrar.");
        }

        private static string GerarToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

        /// <summary>
        /// O banco guarda apenas o hash: quem obtiver acesso à tabela não consegue
        /// reconstruir os links de redefinição em aberto.
        /// </summary>
        private static string CalcularHash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
