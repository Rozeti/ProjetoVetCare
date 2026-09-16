using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>HU-016 e HU-017: indicadores do dia e relatórios de produtividade.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.EquipeClinica)]
    public class DashboardController : ControllerBase
    {
        private readonly ConsultarIndicadoresUseCase _indicadores;
        private readonly GerarRelatorioProdutividadeUseCase _relatorio;

        public DashboardController(
            ConsultarIndicadoresUseCase indicadores,
            GerarRelatorioProdutividadeUseCase relatorio)
        {
            _indicadores = indicadores;
            _relatorio = relatorio;
        }

        /// <summary>HU-016: indicadores operacionais do dia, com escopo definido pela RN-008.</summary>
        [HttpGet("indicadores")]
        public async Task<IActionResult> ObterIndicadores([FromQuery] DateTime? data)
        {
            return this.Responder(await _indicadores.Executar(data));
        }

        /// <summary>HU-017: relatório de produtividade por período.</summary>
        [HttpGet("relatorio-produtividade")]
        [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
        public async Task<IActionResult> ObterRelatorio(
            [FromQuery] DateTime? inicio,
            [FromQuery] DateTime? fim)
        {
            // Sem período informado, o relatório cobre os últimos 30 dias.
            var dataFim = fim ?? DateTime.Now.Date;
            var dataInicio = inicio ?? dataFim.AddDays(-29);

            return this.Responder(await _relatorio.Executar(dataInicio, dataFim));
        }
    }
}
