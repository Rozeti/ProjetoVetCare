using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;

namespace VetCare.API.Controllers
{
    /// <summary>
    /// Configurações da clínica (tenant). Expõe parâmetros operacionais usados pelas
    /// regras de negócio, como a antecedência mínima de cancelamento (RN-009).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClinicaController : ControllerBase
    {
        private readonly IClinicaRepository _clinicas;
        private readonly UsuarioAtual _usuarioAtual;

        public ClinicaController(IClinicaRepository clinicas, UsuarioAtual usuarioAtual)
        {
            _clinicas = clinicas;
            _usuarioAtual = usuarioAtual;
        }

        [HttpGet]
        public async Task<IActionResult> Obter()
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            if (clinica == null)
            {
                return NotFound(new { mensagem = "Clínica não encontrada." });
            }

            return Ok(new ClinicaDTO
            {
                Id = clinica.Id,
                Nome = clinica.Nome,
                Cnpj = clinica.Cnpj,
                Telefone = clinica.Telefone,
                Endereco = clinica.Endereco,
                HorasMinimasCancelamento = clinica.HorasMinimasCancelamento,
                HorarioAbertura = clinica.HorarioAbertura.ToString(@"hh\:mm"),
                HorarioFechamento = clinica.HorarioFechamento.ToString(@"hh\:mm"),
                DuracaoSessaoMinutos = clinica.DuracaoSessaoMinutos
            });
        }

        [HttpPut]
        [Authorize(Roles = Perfis.Administrador)]
        public async Task<IActionResult> Atualizar(AtualizarClinicaDTO dto)
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            if (clinica == null)
            {
                return NotFound(new { mensagem = "Clínica não encontrada." });
            }

            if (!TimeSpan.TryParse(dto.HorarioAbertura, out var abertura) ||
                !TimeSpan.TryParse(dto.HorarioFechamento, out var fechamento))
            {
                return BadRequest(new { mensagem = "Informe os horários no formato HH:mm." });
            }

            if (fechamento <= abertura)
            {
                return BadRequest(new { mensagem = "O horário de fechamento deve ser posterior ao de abertura." });
            }

            clinica.Nome = dto.Nome.Trim();
            clinica.Cnpj = dto.Cnpj.Trim();
            clinica.Telefone = dto.Telefone.Trim();
            clinica.Endereco = dto.Endereco.Trim();
            clinica.HorasMinimasCancelamento = dto.HorasMinimasCancelamento;
            clinica.DuracaoSessaoMinutos = dto.DuracaoSessaoMinutos;
            clinica.HorarioAbertura = abertura;
            clinica.HorarioFechamento = fechamento;

            _clinicas.Atualizar(clinica);
            await _clinicas.SalvarAlteracoes();

            return Ok(new { mensagem = "Configurações da clínica atualizadas com sucesso." });
        }
    }
}
