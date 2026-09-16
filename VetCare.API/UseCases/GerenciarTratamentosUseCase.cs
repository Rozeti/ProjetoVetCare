using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Tratamento é o processo terapêutico que agrupa avaliação, sessões e atendimentos
    /// de um paciente, conforme a definição do DAS.
    /// </summary>
    public class GerenciarTratamentosUseCase
    {
        private static readonly string[] StatusPermitidos = { "Em Andamento", "Concluído", "Interrompido" };

        private readonly ITratamentoRepository _tratamentos;
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IProntuarioRepository _prontuarios;
        private readonly ISessaoRepository _sessoes;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarTratamentosUseCase(
            ITratamentoRepository tratamentos,
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            IProntuarioRepository prontuarios,
            ISessaoRepository sessoes,
            UsuarioAtual usuarioAtual)
        {
            _tratamentos = tratamentos;
            _pets = pets;
            _veterinarios = veterinarios;
            _prontuarios = prontuarios;
            _sessoes = sessoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<TratamentoDTO>> Cadastrar(CriarTratamentoDTO dto)
        {
            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!pet.Ativo)
            {
                return Resultado<TratamentoDTO>.Invalido(
                    "Este paciente está inativo. Reative o cadastro antes de iniciar um tratamento.");
            }

            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<TratamentoDTO>.Invalido("Informe o veterinário responsável pelo tratamento.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            var dataInicio = dto.DataInicio == default
                ? DateTime.UtcNow
                : AgendarSessaoUseCase.NormalizarParaUtc(dto.DataInicio);

            var tratamento = new Tratamento
            {
                PacienteId = dto.PacienteId,
                VeterinarioId = veterinarioId,
                DataInicio = dataInicio,
                ObjetivoTerapeutico = dto.ObjetivoTerapeutico.Trim(),
                ObservacoesGerais = dto.ObservacoesGerais.Trim(),
                Status = "Em Andamento"
            };

            await _tratamentos.Adicionar(tratamento);
            await _tratamentos.SalvarAlteracoes();

            // Garante o prontuário do paciente já na abertura do tratamento.
            await _prontuarios.ObterOuCriarPorPacienteId(dto.PacienteId);

            tratamento.Paciente = pet;
            tratamento.Veterinario = veterinario;

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, 0, 0),
                "Tratamento cadastrado com sucesso.");
        }

        public async Task<Resultado<List<TratamentoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<TratamentoDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (_usuarioAtual.EhTutor && pet.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<List<TratamentoDTO>>.NaoAutorizado("Você não tem acesso a este paciente.");
            }

            var tratamentos = await _tratamentos.ObterPorPaciente(pacienteId);

            return Resultado<List<TratamentoDTO>>.Ok(await MapearLista(tratamentos));
        }

        public async Task<Resultado<List<TratamentoDTO>>> ListarDoVeterinario(Guid? veterinarioId)
        {
            var id = veterinarioId ?? _usuarioAtual.VeterinarioId;

            if (id == null)
            {
                return Resultado<List<TratamentoDTO>>.Invalido("Informe o veterinário.");
            }

            var tratamentos = await _tratamentos.ObterPorVeterinario(id.Value);

            return Resultado<List<TratamentoDTO>>.Ok(await MapearLista(tratamentos));
        }

        public async Task<Resultado<TratamentoDTO>> ObterPorId(Guid id)
        {
            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(id);

            if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Tratamento não encontrado.");
            }

            if (_usuarioAtual.EhTutor && tratamento.Paciente?.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<TratamentoDTO>.NaoAutorizado("Você não tem acesso a este tratamento.");
            }

            var sessoes = await _sessoes.ObterPorTratamento(id);

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, sessoes.Count, sessoes.Count(s => s.Status == "Concluída")));
        }

        public async Task<Resultado<TratamentoDTO>> Atualizar(Guid id, AtualizarTratamentoDTO dto)
        {
            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(id);

            if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Tratamento não encontrado.");
            }

            if (_usuarioAtual.EhVeterinario && tratamento.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado<TratamentoDTO>.NaoAutorizado("Você só pode alterar os seus próprios tratamentos.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Status) && !StatusPermitidos.Contains(dto.Status))
            {
                return Resultado<TratamentoDTO>.Invalido(
                    $"Status inválido. Use um destes: {string.Join(", ", StatusPermitidos)}.");
            }

            if (!string.IsNullOrWhiteSpace(dto.ObjetivoTerapeutico))
            {
                tratamento.ObjetivoTerapeutico = dto.ObjetivoTerapeutico.Trim();
            }

            tratamento.ObservacoesGerais = dto.ObservacoesGerais?.Trim() ?? tratamento.ObservacoesGerais;

            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                tratamento.Status = dto.Status;

                // Encerrar o tratamento sem data explícita registra o encerramento agora.
                if (dto.Status != "Em Andamento" && tratamento.DataFim == null)
                {
                    tratamento.DataFim = dto.DataFim.HasValue
                        ? AgendarSessaoUseCase.NormalizarParaUtc(dto.DataFim.Value)
                        : DateTime.UtcNow;
                }

                if (dto.Status == "Em Andamento")
                {
                    tratamento.DataFim = null;
                }
            }
            else if (dto.DataFim.HasValue)
            {
                tratamento.DataFim = AgendarSessaoUseCase.NormalizarParaUtc(dto.DataFim.Value);
            }

            _tratamentos.Atualizar(tratamento);
            await _tratamentos.SalvarAlteracoes();

            var sessoes = await _sessoes.ObterPorTratamento(id);

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, sessoes.Count, sessoes.Count(s => s.Status == "Concluída")),
                "Tratamento atualizado com sucesso.");
        }

        private async Task<List<TratamentoDTO>> MapearLista(List<Tratamento> tratamentos)
        {
            var lista = new List<TratamentoDTO>(tratamentos.Count);

            foreach (var tratamento in tratamentos)
            {
                var sessoes = await _sessoes.ObterPorTratamento(tratamento.Id);
                lista.Add(MapearParaDTO(tratamento, sessoes.Count, sessoes.Count(s => s.Status == "Concluída")));
            }

            return lista;
        }

        private static TratamentoDTO MapearParaDTO(Tratamento tratamento, int totalSessoes, int sessoesConcluidas)
        {
            return new TratamentoDTO
            {
                Id = tratamento.Id,
                PacienteId = tratamento.PacienteId,
                NomePaciente = tratamento.Paciente?.Nome ?? string.Empty,
                VeterinarioId = tratamento.VeterinarioId,
                NomeVeterinario = tratamento.Veterinario?.Usuario?.Nome ?? string.Empty,
                DataInicio = tratamento.DataInicio,
                DataFim = tratamento.DataFim,
                ObjetivoTerapeutico = tratamento.ObjetivoTerapeutico,
                Status = tratamento.Status,
                ObservacoesGerais = tratamento.ObservacoesGerais,
                TotalSessoes = totalSessoes,
                SessoesConcluidas = sessoesConcluidas
            };
        }
    }
}
