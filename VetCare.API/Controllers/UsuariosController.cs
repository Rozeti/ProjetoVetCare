using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        private readonly GerenciarUsuariosUseCase _gerenciar;
        private readonly AutenticarUsuarioUseCase _autenticar;
        private readonly RecuperarSenhaUseCase _recuperarSenha;
        private readonly UsuarioAtual _usuarioAtual;

        public UsuariosController(
            GerenciarUsuariosUseCase gerenciar,
            AutenticarUsuarioUseCase autenticar,
            RecuperarSenhaUseCase recuperarSenha,
            UsuarioAtual usuarioAtual)
        {
            _gerenciar = gerenciar;
            _autenticar = autenticar;
            _recuperarSenha = recuperarSenha;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>HU-001: autenticação por perfil em tela de login única.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(LimitesDeRequisicao.Autenticacao)]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            var resultado = await _autenticar.Executar(dto);

            return resultado.Sucesso
                ? Ok(resultado.Dados)
                : this.Responder(resultado);
        }

        /// <summary>Dados do usuário autenticado, usados pelo front para restaurar a sessão.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> ObterPerfil()
        {
            return this.Responder(await _gerenciar.ObterPorId(_usuarioAtual.Id));
        }

        [HttpPut("me/senha")]
        public async Task<IActionResult> AlterarPropriaSenha(AlterarSenhaDTO dto)
        {
            return this.Responder(await _gerenciar.AlterarPropriaSenha(dto));
        }

        /// <summary>HU-015: liga ou desliga o aviso por e-mail e no celular para a própria conta.</summary>
        [HttpPut("me/preferencias-de-notificacao")]
        public async Task<IActionResult> AtualizarPreferenciasDeNotificacao(PreferenciasDeNotificacaoDTO dto)
        {
            return this.Responder(await _gerenciar.AtualizarPreferenciasDeNotificacao(dto));
        }

        /// <summary>
        /// "Esqueci minha senha": envia ao e-mail cadastrado um link e um código de uso único.
        /// A resposta é a mesma exista a conta ou não.
        /// </summary>
        [HttpPost("recuperar-senha")]
        [AllowAnonymous]
        [EnableRateLimiting(LimitesDeRequisicao.Autenticacao)]
        public async Task<IActionResult> SolicitarRecuperacao(SolicitarRecuperacaoDTO dto)
        {
            return this.Responder(await _recuperarSenha.Solicitar(dto));
        }

        /// <summary>Define a nova senha com o token do link ou com o e-mail e o código recebidos.</summary>
        [HttpPost("redefinir-senha")]
        [AllowAnonymous]
        [EnableRateLimiting(LimitesDeRequisicao.Autenticacao)]
        public async Task<IActionResult> RedefinirSenha(RedefinirSenhaDTO dto)
        {
            return this.Responder(await _recuperarSenha.Redefinir(dto));
        }

        /// <summary>HU-002: listagem de usuários da clínica, aberta ao Administrador e ao Apoio.</summary>
        [HttpGet]
        [Authorize(Roles = Perfis.AdministradorOuApoio)]
        public async Task<IActionResult> Listar(
            [FromQuery] string? perfil,
            [FromQuery] string? busca,
            [FromQuery] bool? ativo,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _gerenciar.Listar(perfil, busca, ativo, paginacao));
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuApoio)]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _gerenciar.ObterPorId(id));
        }

        /// <summary>HU-002, CA-1: cadastro de usuário com definição de perfil.</summary>
        [HttpPost]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Cadastrar(CriarUsuarioDTO dto)
        {
            // Todo usuário nasce na clínica de quem o cadastra; um token sem clínica não
            // pode escolher uma "qualquer" para o novo usuário.
            if (_usuarioAtual.ClinicaId == Guid.Empty)
            {
                return BadRequest(new { mensagem = "A sessão atual não está vinculada a uma clínica. Entre novamente." });
            }

            return this.ResponderCriado(await _gerenciar.Cadastrar(dto, _usuarioAtual.ClinicaId));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarUsuarioDTO dto)
        {
            return this.Responder(await _gerenciar.Atualizar(id, dto));
        }

        /// <summary>HU-002, CA-4: ativa ou desativa a conta sem excluir dados.</summary>
        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusDTO dto)
        {
            return this.Responder(await _gerenciar.AlterarStatus(id, dto.Ativo));
        }

        /// <summary>HU-002, CA-5: gera uma nova senha provisória e a envia por e-mail ao usuário.</summary>
        [HttpPost("{id:guid}/redefinir-senha")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> RedefinirSenhaDoUsuario(Guid id)
        {
            return this.Responder(await _gerenciar.RedefinirSenha(id));
        }
    }
}
