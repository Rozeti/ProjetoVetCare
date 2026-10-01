using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Configurações da clínica (tenant): parâmetros operacionais usados pelas regras de
    /// negócio, como a antecedência mínima de cancelamento (RN-009) e o expediente (HU-004).
    /// </summary>
    public class GerenciarClinicaUseCase
    {
        private readonly IClinicaRepository _clinicas;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarClinicaUseCase(IClinicaRepository clinicas, UsuarioAtual usuarioAtual)
        {
            _clinicas = clinicas;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<ClinicaDTO>> Obter()
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            return clinica == null
                ? Resultado<ClinicaDTO>.NaoEncontrado("Clínica não encontrada.")
                : Resultado<ClinicaDTO>.Ok(MapearParaDTO(clinica));
        }

        public async Task<Resultado<ClinicaDTO>> Atualizar(AtualizarClinicaDTO dto)
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            if (clinica == null)
            {
                return Resultado<ClinicaDTO>.NaoEncontrado("Clínica não encontrada.");
            }

            if (!TimeSpan.TryParse(dto.HorarioAbertura, out var abertura) ||
                !TimeSpan.TryParse(dto.HorarioFechamento, out var fechamento))
            {
                return Resultado<ClinicaDTO>.Invalido("Informe os horários no formato HH:mm.");
            }

            if (fechamento <= abertura)
            {
                return Resultado<ClinicaDTO>.Invalido("O horário de fechamento deve ser posterior ao de abertura.");
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

            return Resultado<ClinicaDTO>.Ok(MapearParaDTO(clinica), "Configurações da clínica atualizadas com sucesso.");
        }

        private static ClinicaDTO MapearParaDTO(Clinica clinica) => new()
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
        };
    }
}
