using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Common;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.UseCases;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// HU-009: observações internas do prontuário.
    /// RN-003 e RNF-003: a rota inteira é fechada para o perfil Tutor. Além disso, o caso
    /// de uso repete a verificação, para que a restrição não dependa apenas da rota.
    /// </summary>
    [ApiController]
    [Route("api/observacoes-internas")]
    [Authorize(Roles = Perfis.AdministradorOuVeterinario)]
    public class ObservacoesInternasController : ControllerBase
    {
        private readonly RegistrarObservacaoInternaUseCase _useCase;

        public ObservacoesInternasController(RegistrarObservacaoInternaUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpPost]
        public async Task<IActionResult> Registrar(CriarObservacaoInternaDTO dto)
        {
            return this.ResponderCriado(await _useCase.Executar(dto));
        }

        [HttpGet("paciente/{pacienteId:guid}")]
        public async Task<IActionResult> ListarPorPaciente(Guid pacienteId)
        {
            return this.Responder(await _useCase.ListarPorPaciente(pacienteId));
        }
    }
}
