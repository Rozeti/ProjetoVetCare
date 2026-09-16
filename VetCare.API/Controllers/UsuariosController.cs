using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VetCare.API.Common;
using VetCare.API.Data;
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
        private readonly IClinicaRepository _clinicas;
        private readonly UsuarioAtual _usuarioAtual;

        public UsuariosController(
            GerenciarUsuariosUseCase gerenciar,
            AutenticarUsuarioUseCase autenticar,
            RecuperarSenhaUseCase recuperarSenha,
            IClinicaRepository clinicas,
            UsuarioAtual usuarioAtual)
        {
            _gerenciar = gerenciar;
            _autenticar = autenticar;
            _recuperarSenha = recuperarSenha;
            _clinicas = clinicas;
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

        /// <summary>Redefinição de senha solicitada pelo próprio usuário.</summary>
        [HttpPost("recuperar-senha")]
        [AllowAnonymous]
        [EnableRateLimiting(LimitesDeRequisicao.Autenticacao)]
        public async Task<IActionResult> SolicitarRecuperacao(SolicitarRecuperacaoDTO dto)
        {
            return this.Responder(await _recuperarSenha.Solicitar(dto));
        }

        [HttpPost("redefinir-senha")]
        [AllowAnonymous]
        [EnableRateLimiting(LimitesDeRequisicao.Autenticacao)]
        public async Task<IActionResult> RedefinirComToken(RedefinirComTokenDTO dto)
        {
            return this.Responder(await _recuperarSenha.Redefinir(dto));
        }

        /// <summary>HU-002: listagem de usuários da clínica, restrita ao Administrador.</summary>
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
            var clinicaId = _usuarioAtual.ClinicaId;

            if (clinicaId == Guid.Empty)
            {
                var clinica = await _clinicas.ObterPrimeira();

                if (clinica == null)
                {
                    return BadRequest(new { mensagem = "Nenhuma clínica cadastrada no sistema." });
                }

                clinicaId = clinica.Id;
            }

            return this.ResponderCriado(await _gerenciar.Cadastrar(dto, clinicaId));
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
        public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusUsuarioDTO dto)
        {
            return this.Responder(await _gerenciar.AlterarStatus(id, dto.Ativo));
        }

        /// <summary>HU-002, CA-5: gera uma nova senha provisória para o usuário.</summary>
        [HttpPost("{id:guid}/redefinir-senha")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> RedefinirSenha(Guid id)
        {
            return this.Responder(await _gerenciar.RedefinirSenha(id));
        }
    }
}
