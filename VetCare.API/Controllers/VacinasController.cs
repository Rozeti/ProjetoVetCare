using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>Carteira de vacinação e vermifugação dos pacientes.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VacinasController : ControllerBase
    {
        private readonly GerenciarVacinasUseCase _useCase;

        public VacinasController(GerenciarVacinasUseCase useCase) => _useCase = useCase;

        /// <summary>
        /// Carteira do paciente, paginada (RNF-004). Filtros: <c>tipo</c> (Vacina, Vermifugo,
        /// Antipulgas, Outro), <c>situacao</c> (Em dia, A vencer, Vencida, Dose única,
        /// Concluída) e <c>busca</c> (produto, fabricante ou lote).
        /// </summary>
        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(
            Guid pacienteId,
            [FromQuery] string? tipo,
            [FromQuery] string? situacao,
            [FromQuery] string? busca,
            [FromQuery] ParametrosPagina paginacao)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId, tipo, situacao, busca, paginacao));
        }

        /// <summary>Painel de prevenção: doses que vencem na janela informada.</summary>
        [HttpGet("vencendo")]
        [Authorize(Roles = Perfis.EquipeClinica)]
        public async Task<IActionResult> ListarVencendo([FromQuery] int dias = VacinaRepository.DiasDeAntecedenciaDoAviso)
        {
            return this.Responder(await _useCase.ListarVencendo(dias));
        }

        [HttpPost]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Registrar(CriarVacinaDTO dto)
        {
            return this.ResponderCriado(await _useCase.Registrar(dto));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Atualizar(Guid id, AtualizarVacinaDTO dto)
        {
            return this.Responder(await _useCase.Atualizar(id, dto));
        }

        /// <summary>
        /// O veterinário responsável pelo paciente e a administração apagam um registro
        /// lançado por engano. A justificativa vai no corpo e fica na auditoria.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> Remover(
            Guid id,
            [FromBody] ExcluirVacinaDTO? dto,
            [FromQuery] string? justificativa = null)
        {
            // O corpo de um DELETE pode ser descartado por intermediários; a query serve de alternativa.
            return this.Responder(await _useCase.Remover(id, dto?.Justificativa ?? justificativa ?? string.Empty));
        }
    }
}
