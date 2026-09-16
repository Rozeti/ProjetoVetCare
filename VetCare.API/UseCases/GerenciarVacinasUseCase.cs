using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Carteira de vacinação e vermifugação do paciente. O controle da próxima dose
    /// é o que sustenta a rotina de prevenção acompanhada pelo tutor.
    /// </summary>
    public class GerenciarVacinasUseCase
    {
        private static readonly string[] TiposValidos = { "Vacina", "Vermifugo", "Antipulgas", "Outro" };

        private readonly IVacinaRepository _vacinas;
        private readonly IPetRepository _pets;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarVacinasUseCase(IVacinaRepository vacinas, IPetRepository pets, UsuarioAtual usuarioAtual)
        {
            _vacinas = vacinas;
            _pets = pets;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<VacinaDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<VacinaDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (_usuarioAtual.EhTutor && pet.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<List<VacinaDTO>>.NaoAutorizado("Você não tem acesso a este paciente.");
            }

            var vacinas = await _vacinas.ObterPorPaciente(pacienteId);

            return Resultado<List<VacinaDTO>>.Ok(vacinas.Select(MapearParaDTO).ToList());
        }

        /// <summary>Painel de prevenção da clínica: o que vence nos próximos dias.</summary>
        public async Task<Resultado<List<VacinaDTO>>> ListarVencendo(int dias)
        {
            var janela = Math.Clamp(dias, 1, 365);
            var vacinas = await _vacinas.ObterVencendo(_usuarioAtual.ClinicaId, janela);

            return Resultado<List<VacinaDTO>>.Ok(vacinas.Select(MapearParaDTO).ToList());
        }

        public async Task<Resultado<VacinaDTO>> Registrar(CriarVacinaDTO dto)
        {
            if (!TiposValidos.Contains(dto.Tipo, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<VacinaDTO>.Invalido(
                    $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}.");
            }

            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<VacinaDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            var aplicacao = DateTime.SpecifyKind(dto.DataAplicacao.Date, DateTimeKind.Utc);

            if (aplicacao > DateTime.UtcNow.Date)
            {
                return Resultado<VacinaDTO>.Invalido("A data de aplicação não pode ser futura.");
            }

            DateTime? proxima = null;

            if (dto.ProximaDose.HasValue)
            {
                proxima = DateTime.SpecifyKind(dto.ProximaDose.Value.Date, DateTimeKind.Utc);

                if (proxima <= aplicacao)
                {
                    return Resultado<VacinaDTO>.Invalido("A próxima dose deve ser posterior à data de aplicação.");
                }
            }

            var vacina = new Vacina
            {
                PacienteId = dto.PacienteId,
                VeterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId,
                Tipo = Normalizar(dto.Tipo),
                Nome = dto.Nome.Trim(),
                Fabricante = dto.Fabricante.Trim(),
                Lote = dto.Lote.Trim(),
                DataAplicacao = aplicacao,
                ProximaDose = proxima,
                Observacoes = dto.Observacoes.Trim()
            };

            await _vacinas.Adicionar(vacina);
            await _vacinas.SalvarAlteracoes();

            vacina.Paciente = pet;

            return Resultado<VacinaDTO>.Ok(MapearParaDTO(vacina), "Registro adicionado à carteira do paciente.");
        }

        public async Task<Resultado<VacinaDTO>> Atualizar(Guid id, AtualizarVacinaDTO dto)
        {
            var vacina = await _vacinas.ObterPorId(id);

            if (vacina == null || vacina.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<VacinaDTO>.NaoEncontrado("Registro não encontrado.");
            }

            if (!TiposValidos.Contains(dto.Tipo, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<VacinaDTO>.Invalido(
                    $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}.");
            }

            var aplicacao = DateTime.SpecifyKind(dto.DataAplicacao.Date, DateTimeKind.Utc);
            var proxima = dto.ProximaDose.HasValue
                ? DateTime.SpecifyKind(dto.ProximaDose.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;

            if (proxima.HasValue && proxima <= aplicacao)
            {
                return Resultado<VacinaDTO>.Invalido("A próxima dose deve ser posterior à data de aplicação.");
            }

            vacina.Tipo = Normalizar(dto.Tipo);
            vacina.Nome = dto.Nome.Trim();
            vacina.Fabricante = dto.Fabricante.Trim();
            vacina.Lote = dto.Lote.Trim();
            vacina.DataAplicacao = aplicacao;
            vacina.Observacoes = dto.Observacoes.Trim();

            // Uma nova data reabre o lembrete, que pode já ter sido enviado antes.
            if (vacina.ProximaDose != proxima)
            {
                vacina.ProximaDose = proxima;
                vacina.LembreteEnviado = false;
            }

            _vacinas.Atualizar(vacina);
            await _vacinas.SalvarAlteracoes();

            return Resultado<VacinaDTO>.Ok(MapearParaDTO(vacina), "Registro atualizado.");
        }

        public async Task<Resultado> Remover(Guid id)
        {
            var vacina = await _vacinas.ObterPorId(id);

            if (vacina == null || vacina.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Registro não encontrado.");
            }

            _vacinas.Remover(vacina);
            await _vacinas.SalvarAlteracoes();

            return Resultado.Ok("Registro removido da carteira.");
        }

        private static string Normalizar(string tipo) =>
            TiposValidos.FirstOrDefault(t => string.Equals(t, tipo, StringComparison.OrdinalIgnoreCase)) ?? tipo;

        public static VacinaDTO MapearParaDTO(Vacina vacina)
        {
            var dias = vacina.ProximaDose.HasValue
                ? (int)(vacina.ProximaDose.Value.Date - DateTime.UtcNow.Date).TotalDays
                : (int?)null;

            return new VacinaDTO
            {
                Id = vacina.Id,
                PacienteId = vacina.PacienteId,
                NomePaciente = vacina.Paciente?.Nome ?? string.Empty,
                NomeTutor = vacina.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                Tipo = vacina.Tipo,
                Nome = vacina.Nome,
                Fabricante = vacina.Fabricante,
                Lote = vacina.Lote,
                DataAplicacao = vacina.DataAplicacao,
                ProximaDose = vacina.ProximaDose,
                Observacoes = vacina.Observacoes,
                AplicadaPor = vacina.Veterinario?.Usuario?.Nome ?? string.Empty,
                SituacaoDose = ClassificarSituacao(dias),
                DiasParaProximaDose = dias
            };
        }

        /// <summary>
        /// Classificação usada pela interface para destacar o que exige ação: a
        /// janela de 30 dias dá tempo de agendar a aplicação antes do vencimento.
        /// </summary>
        private static string ClassificarSituacao(int? diasParaProximaDose) => diasParaProximaDose switch
        {
            null => "Dose única",
            < 0 => "Vencida",
            <= 30 => "A vencer",
            _ => "Em dia"
        };
    }
}
