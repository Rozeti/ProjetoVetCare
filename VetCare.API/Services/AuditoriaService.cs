using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.Services
{
    /// <summary>
    /// Trilha de auditoria dos acessos e alterações em dados clínicos. Registrar quem
    /// consultou o quê é exigência de rastreabilidade em prontuário e base para
    /// responder a pedidos de titulares sobre o tratamento dos seus dados.
    /// </summary>
    public class AuditoriaService
    {
        private readonly IAuditoriaRepository _repositorio;
        private readonly UsuarioAtual _usuarioAtual;
        private readonly IHttpContextAccessor _accessor;
        private readonly ILogger<AuditoriaService> _logger;

        public AuditoriaService(
            IAuditoriaRepository repositorio,
            UsuarioAtual usuarioAtual,
            IHttpContextAccessor accessor,
            ILogger<AuditoriaService> logger)
        {
            _repositorio = repositorio;
            _usuarioAtual = usuarioAtual;
            _accessor = accessor;
            _logger = logger;
        }

        public static class Acoes
        {
            public const string Login = "Login";
            public const string LoginNegado = "LoginNegado";
            public const string Consulta = "Consulta";
            public const string Criacao = "Criacao";
            public const string Alteracao = "Alteracao";
            public const string Inativacao = "Inativacao";
            public const string Download = "Download";
            public const string RedefinicaoSenha = "RedefinicaoSenha";
        }

        public Task RegistrarDoUsuarioAtual(string acao, string entidade, Guid? entidadeId = null, string detalhe = "")
        {
            return Registrar(
                _usuarioAtual.ClinicaId,
                _usuarioAtual.Id,
                _usuarioAtual.Nome,
                _usuarioAtual.Perfil,
                acao,
                entidade,
                entidadeId,
                detalhe);
        }

        public Task RegistrarDe(Usuario usuario, string acao, string entidade, Guid? entidadeId = null, string detalhe = "")
        {
            return Registrar(
                usuario.ClinicaId,
                usuario.Id,
                usuario.Nome,
                usuario.Perfil,
                acao,
                entidade,
                entidadeId,
                detalhe);
        }

        /// <summary>Tentativa de acesso sem usuário identificado: só temos o e-mail informado.</summary>
        public Task RegistrarTentativaAnonima(string acao, string emailInformado)
        {
            return Registrar(
                Guid.Empty,
                Guid.Empty,
                emailInformado,
                "Desconhecido",
                acao,
                "Autenticacao",
                null,
                $"Tentativa com o e-mail {emailInformado}");
        }

        private async Task Registrar(
            Guid clinicaId,
            Guid usuarioId,
            string nomeUsuario,
            string perfil,
            string acao,
            string entidade,
            Guid? entidadeId,
            string detalhe)
        {
            try
            {
                await _repositorio.Registrar(new RegistroAuditoria
                {
                    ClinicaId = clinicaId,
                    UsuarioId = usuarioId,
                    NomeUsuario = Limitar(nomeUsuario, 120),
                    Perfil = Limitar(perfil, 20),
                    Acao = acao,
                    Entidade = entidade,
                    EntidadeId = entidadeId,
                    Detalhe = Limitar(detalhe, 500),
                    EnderecoIp = ObterEnderecoIp()
                });
            }
            catch (Exception excecao)
            {
                // A auditoria acompanha a operação, mas não pode derrubá-la: uma falha
                // ao gravar o rastro não deve impedir o atendimento de acontecer.
                _logger.LogError(excecao, "Falha ao gravar o registro de auditoria da ação {Acao}.", acao);
            }
        }

        private string ObterEnderecoIp()
        {
            var contexto = _accessor.HttpContext;

            if (contexto == null)
            {
                return string.Empty;
            }

            // Atrás de proxy reverso o IP real chega no cabeçalho encaminhado.
            var encaminhado = contexto.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(encaminhado))
            {
                return Limitar(encaminhado.Split(',')[0].Trim(), 60);
            }

            return Limitar(contexto.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 60);
        }

        private static string Limitar(string valor, int tamanho) =>
            string.IsNullOrEmpty(valor) || valor.Length <= tamanho ? valor : valor[..tamanho];
    }
}
