using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-003: gestão dos pacientes (pets) da clínica.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PetsController : ControllerBase
    {
        private readonly GerenciarPacientesUseCase _useCase;

        public PetsController(GerenciarPacientesUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] Guid? tutorId,
            [FromQuery] string? busca,
            [FromQuery] bool? ativo,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _useCase.Listar(tutorId, busca, ativo, paginacao));
        }

        /// <summary>
        /// HU-013, CA-1: lista apenas os pets sob responsabilidade do tutor logado.
        /// Devolve o array direto porque um tutor tem poucos pets e o aplicativo
        /// exibe todos de uma vez.
        /// </summary>
        [HttpGet("meus")]
        [Authorize(Roles = Perfis.Tutor)]
        public async Task<IActionResult> ListarMeusPets()
        {
            var resultado = await _useCase.Listar(null, null, true, new ParametrosPagina { Tamanho = 100 });

            return resultado.Sucesso
                ? Ok(resultado.Dados!.Itens)
                : this.Responder(resultado);
        }

        /// <summary>
        /// Cadastro do próprio tutor, para o animal recém-adquirido que ainda não existe no
        /// sistema. O responsável é deduzido do token, e não do corpo da requisição.
        /// </summary>
        [HttpPost("meus")]
        [Authorize(Roles = Perfis.Tutor)]
        public async Task<IActionResult> CadastrarMeuPet(CriarPetDoTutorDTO dto)
        {
            return this.ResponderCriado(await _useCase.CadastrarComoTutor(dto));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Cadastrar(CriarPetDTO dto)
        {
            return this.ResponderCriado(await _useCase.Cadastrar(dto));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarPetDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }

        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> AlterarStatus(Guid id, [FromBody] AlterarStatusUsuarioDTO dto)
        {
            return this.Responder(await _useCase.AlterarStatus(id, dto.Ativo));
        }

        /// <summary>
        /// HU-003, CA-4: pacientes com prontuário não são excluídos; a operação é recusada
        /// e o sistema oferece apenas a inativação.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Excluir(Guid id)
        {
            return this.Responder(await _useCase.Excluir(id));
        }
    }
}
