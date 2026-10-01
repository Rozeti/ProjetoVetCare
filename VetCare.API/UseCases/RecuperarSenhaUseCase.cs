using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// "Esqueci minha senha": o usuário informa o e-mail cadastrado e recebe um link (para o
    /// portal) e um código de seis dígitos (para o aplicativo). Qualquer um dos dois define a
    /// nova senha, uma única vez e dentro do prazo. O mesmo caminho conclui o primeiro acesso
    /// de uma conta criada pela clínica.
    /// </summary>
    public class RecuperarSenhaUseCase
    {
        private const string MensagemNeutra =
            "Se houver uma conta ativa com este e-mail, enviaremos as instruções de redefinição.";

        private readonly IUsuarioRepository _usuarios;
        private readonly ITokenRedefinicaoRepository _tokens;
        private readonly PasswordHasher _hasher;
        private readonly AuditoriaService _auditoria;
        private readonly ContasService _contas;

        public RecuperarSenhaUseCase(
            IUsuarioRepository usuarios,
            ITokenRedefinicaoRepository tokens,
            PasswordHasher hasher,
            AuditoriaService auditoria,
            ContasService contas)
        {
            _usuarios = usuarios;
            _tokens = tokens;
            _hasher = hasher;
            _auditoria = auditoria;
            _contas = contas;
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

            var credenciais = await _contas.EnviarRecuperacaoDeSenha(usuario);

            await _auditoria.RegistrarDe(
                usuario,
                AuditoriaService.Acoes.SolicitacaoDeSenha,
                "Usuario",
                usuario.Id,
                "Pedido de redefinição de senha enviado para o e-mail cadastrado");

            if (_contas.ExporCredenciaisNaResposta)
            {
                resposta.TokenDesenvolvimento = credenciais.Token;
                resposta.CodigoDesenvolvimento = credenciais.Codigo;
            }

            return Resultado<RespostaRecuperacaoDTO>.Ok(resposta);
        }

        public async Task<Resultado> Redefinir(RedefinirSenhaDTO dto)
        {
            var (pedido, falha) = await LocalizarPedido(dto);

            if (falha != null || pedido?.Usuario == null)
            {
                return falha ?? Resultado.Invalido("Link ou código inválido ou expirado. Solicite um novo.");
            }

            var usuario = pedido.Usuario;

            if (!usuario.Ativo)
            {
                return Resultado.Invalido("Esta conta está inativa. Procure o administrador da clínica.");
            }

            usuario.SenhaHash = _hasher.Gerar(dto.NovaSenha);

            // RN-006: a redefinição libera a conta de um bloqueio por tentativas.
            usuario.TentativasFalhas = 0;
            usuario.BloqueadoAte = null;

            pedido.UtilizadoEm = DateTime.UtcNow;

            _usuarios.Atualizar(usuario);
            _tokens.Atualizar(pedido);
            await _tokens.SalvarAlteracoes();

            var primeiroAcesso = pedido.Finalidade == FinalidadesDoToken.PrimeiroAcesso;

            await _auditoria.RegistrarDe(
                usuario,
                AuditoriaService.Acoes.RedefinicaoSenha,
                "Usuario",
                usuario.Id,
                primeiroAcesso ? "Senha criada no primeiro acesso" : "Senha redefinida pelo próprio usuário");

            _contas.EnviarAvisoDeSenhaAlterada(usuario);

            return Resultado.Ok(primeiroAcesso
                ? "Senha criada com sucesso. Use-a para entrar."
                : "Senha redefinida com sucesso. Use a nova senha para entrar.");
        }

        /// <summary>
        /// Encontra o pedido pelo token do link ou pelo par e-mail + código. As mensagens de
        /// erro são genéricas de propósito: não dizem se o e-mail existe nem se há pedido aberto.
        /// </summary>
        private async Task<(TokenRedefinicaoSenha? Pedido, Resultado? Falha)> LocalizarPedido(RedefinirSenhaDTO dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.Token))
            {
                var pedido = await _tokens.ObterPorHash(CredenciaisTemporarias.HashDoToken(dto.Token.Trim()));

                return pedido == null || !pedido.Valido
                    ? (null, Resultado.Invalido("Link de redefinição inválido ou expirado. Solicite um novo."))
                    : (pedido, null);
            }

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Codigo))
            {
                return (null, Resultado.Invalido("Informe o link recebido por e-mail, ou o e-mail da conta e o código."));
            }

            var usuario = await _usuarios.ObterPorEmail(dto.Email);
            var aberto = usuario == null ? null : await _tokens.ObterAbertoDoUsuario(usuario.Id);

            if (aberto == null || !aberto.Valido)
            {
                return (null, Resultado.Invalido("Código inválido ou expirado. Solicite um novo."));
            }

            if (!CredenciaisTemporarias.CodigoConfere(aberto.Id, dto.Codigo, aberto.CodigoHash))
            {
                aberto.TentativasDeCodigo++;
                _tokens.Atualizar(aberto);
                await _tokens.SalvarAlteracoes();

                var restantes = TokenRedefinicaoSenha.MaximoTentativasDeCodigo - aberto.TentativasDeCodigo;

                return (null, Resultado.Invalido(restantes > 0
                    ? $"Código incorreto. Você ainda tem {restantes} tentativa(s)."
                    : "Código incorreto. O número de tentativas se esgotou; solicite um novo código."));
            }

            return (aberto, null);
        }
    }
}
