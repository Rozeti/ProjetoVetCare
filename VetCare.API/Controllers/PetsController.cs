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

        public PetsController(GerenciarPacientesUseCase useCase) => _useCase = useCase;

        /// <summary>
        /// O tutor vê só os próprios pets; a equipe clínica (administração, apoio e todos os
        /// veterinários) vê a clínica inteira e pode filtrar por veterinário responsável ou pelos
        /// pacientes ainda sem responsável designado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] Guid? tutorId,
            [FromQuery] Guid? veterinarioId,
            [FromQuery] bool? semResponsavel,
            [FromQuery] string? busca,
            [FromQuery] bool? ativo,
            [FromQuery] string? raca,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _useCase.Listar(tutorId, veterinarioId, semResponsavel == true, busca, ativo, paginacao, raca));
        }

        /// <summary>Raças dos pacientes no recorte de quem consulta, para o filtro da lista.</summary>
        [HttpGet("racas")]
        public async Task<IActionResult> ListarRacas()
        {
            return this.Responder(await _useCase.ListarRacas());
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
            var resultado = await _useCase.Listar(null, null, false, null, true, new ParametrosPagina { Tamanho = 100 });

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

        /// <summary>Pacientes ativos para os seletores dos formulários, no mesmo recorte da listagem e sem paginação.</summary>
        [HttpGet("selecao")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> ListarParaSelecao()
        {
            return this.Responder(await _useCase.ListarParaSelecao());
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> ObterPorId(Guid id)
        {
            return this.Responder(await _useCase.ObterPorId(id));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuApoio)]
        public async Task<IActionResult> Cadastrar(CriarPetDTO dto)
        {
            return this.ResponderCriado(await _useCase.Cadastrar(dto));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuApoio)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarPetDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }

        /// <summary>
        /// HU-003, CA-4: o paciente não é excluído, é inativado. O cadastro e todo o histórico
        /// clínico permanecem (RN-004), e o paciente pode ser reativado depois.
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = Perfis.AdministradorOuApoio)]
        public async Task<IActionResult> AlterarStatus(Guid id, [FromBody] AlterarStatusDTO dto)
        {
            return this.Responder(await _useCase.AlterarStatus(id, dto.Ativo));
        }
    }
}
