using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Tratamento é o processo terapêutico que agrupa avaliação, sessões e atendimentos
    /// de um paciente, conforme a definição do DAS. Um tratamento concluído é
    /// definitivo; um interrompido pode ser retomado.
    /// </summary>
    public class GerenciarTratamentosUseCase
    {
        private readonly ITratamentoRepository _tratamentos;
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IProntuarioRepository _prontuarios;
        private readonly ISessaoRepository _sessoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarTratamentosUseCase(
            ITratamentoRepository tratamentos,
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            IProntuarioRepository prontuarios,
            ISessaoRepository sessoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _tratamentos = tratamentos;
            _pets = pets;
            _veterinarios = veterinarios;
            _prontuarios = prontuarios;
            _sessoes = sessoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<TratamentoDTO>> Cadastrar(CriarTratamentoDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ObjetivoTerapeutico))
            {
                return Resultado<TratamentoDTO>.Invalido("Descreva o objetivo terapêutico do tratamento.");
            }

            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<TratamentoDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (!pet.EmAcompanhamento)
            {
                return Resultado<TratamentoDTO>.Invalido(
                    "Este paciente está inativo ou tem óbito registrado; não é possível iniciar um tratamento.");
            }

            // O tratamento é conduzido pelo veterinário responsável pelo paciente. Sem ninguém
            // designado ainda (cadastro vindo do aplicativo), o primeiro tratamento define quem é.
            var veterinarioId = dto.VeterinarioId
                                ?? pet.VeterinarioResponsavelId
                                ?? _usuarioAtual.VeterinarioId
                                ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<TratamentoDTO>.Invalido("Informe o veterinário responsável pelo tratamento.");
            }

            if (pet.VeterinarioResponsavelId.HasValue && pet.VeterinarioResponsavelId.Value != veterinarioId)
            {
                return Resultado<TratamentoDTO>.Invalido(
                    $"O veterinário responsável por {pet.Nome} é {pet.VeterinarioResponsavel?.Usuario?.Nome ?? "outro profissional"}. " +
                    "Transfira o paciente antes de abrir um tratamento com outro veterinário.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            // Sem data o tratamento começa agora; com data, ela não pode estar no futuro.
            var dataInicio = dto.DataInicio == default
                ? DateTime.UtcNow
                : AgendarSessaoUseCase.NormalizarParaUtc(dto.DataInicio);

            if (RelogioDaClinica.Padrao.DiaDaClinica(dataInicio) > RelogioDaClinica.Padrao.Hoje)
            {
                return Resultado<TratamentoDTO>.Invalido("A data de início do tratamento não pode ser futura.");
            }

            if (!pet.VeterinarioResponsavelId.HasValue)
            {
                pet.VeterinarioResponsavelId = veterinario.Id;
                pet.VeterinarioResponsavel = veterinario;
                _pets.Atualizar(pet);
            }

            var tratamento = new Tratamento
            {
                PacienteId = dto.PacienteId,
                VeterinarioId = veterinarioId,
                DataInicio = dataInicio,
                ObjetivoTerapeutico = dto.ObjetivoTerapeutico.Trim(),
                ObservacoesGerais = dto.ObservacoesGerais.Trim(),
                Status = StatusTratamento.EmAndamento
            };

            await _tratamentos.Adicionar(tratamento);
            await _tratamentos.SalvarAlteracoes();

            // Garante o prontuário do paciente já na abertura do tratamento.
            await _prontuarios.ObterOuCriarPorPacienteId(dto.PacienteId);

            tratamento.Paciente = pet;
            tratamento.Veterinario = veterinario;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Tratamento", tratamento.Id,
                $"Tratamento de {pet.Nome} com {veterinario.Usuario?.Nome}: {tratamento.ObjetivoTerapeutico}");

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, new ContagemDeSessoes(0, 0)),
                "Tratamento cadastrado com sucesso.");
        }

        public async Task<Resultado<List<TratamentoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<TratamentoDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<List<TratamentoDTO>>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var tratamentos = await _tratamentos.ObterPorPaciente(pacienteId);

            return Resultado<List<TratamentoDTO>>.Ok(await MapearLista(tratamentos));
        }

        /// <summary>Tratamentos sob responsabilidade do veterinário autenticado.</summary>
        public async Task<Resultado<List<TratamentoDTO>>> ListarDoVeterinario()
        {
            if (_usuarioAtual.VeterinarioId == null)
            {
                return Resultado<List<TratamentoDTO>>.NaoEncontrado(
                    "Cadastro de veterinário não encontrado para este usuário.");
            }

            var tratamentos = await _tratamentos.ObterPorVeterinario(_usuarioAtual.VeterinarioId.Value);

            return Resultado<List<TratamentoDTO>>.Ok(await MapearLista(tratamentos));
        }

        public async Task<Resultado<TratamentoDTO>> ObterPorId(Guid id)
        {
            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(id);

            if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Tratamento não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, tratamento.Paciente))
            {
                return Resultado<TratamentoDTO>.NaoAutorizado("Você não tem acesso a este tratamento.");
            }

            return Resultado<TratamentoDTO>.Ok(MapearParaDTO(tratamento, await ContarSessoes(id)));
        }

        /// <summary>Campos omitidos (nulos) mantêm o valor atual; só o que veio preenchido é alterado.</summary>
        public async Task<Resultado<TratamentoDTO>> Atualizar(Guid id, AtualizarTratamentoDTO dto)
        {
            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(id);

            if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Tratamento não encontrado.");
            }

            // A mesma regra do restante do prontuário: o veterinário responsável e a administração.
            if (!AcessoAoPaciente.Permitido(_usuarioAtual, tratamento.Paciente))
            {
                return Resultado<TratamentoDTO>.NaoAutorizado("Você não tem acesso a este tratamento.");
            }

            string? novoStatus = null;

            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                novoStatus = StatusTratamento.Normalizar(dto.Status);

                if (novoStatus == null)
                {
                    return Resultado<TratamentoDTO>.Invalido(
                        $"Status inválido. Use um destes: {string.Join(", ", StatusTratamento.Todos)}.");
                }

                // Concluído é definitivo: o histórico do tratamento se encerra ali. Um tratamento
                // interrompido pode voltar a andar.
                if (tratamento.Status == StatusTratamento.Concluido && novoStatus != StatusTratamento.Concluido)
                {
                    return Resultado<TratamentoDTO>.Conflito(
                        "Um tratamento concluído não pode ser reaberto. Inicie um novo tratamento para o paciente.");
                }

                if (novoStatus == StatusTratamento.EmAndamento && tratamento.Status != StatusTratamento.EmAndamento
                    && !tratamento.Paciente!.EmAcompanhamento)
                {
                    return Resultado<TratamentoDTO>.Conflito(
                        "O paciente está inativo ou tem óbito registrado; o tratamento não pode ser retomado.");
                }
            }

            if (dto.ObjetivoTerapeutico != null && string.IsNullOrWhiteSpace(dto.ObjetivoTerapeutico))
            {
                return Resultado<TratamentoDTO>.Invalido("O objetivo terapêutico não pode ficar em branco.");
            }

            if (!string.IsNullOrWhiteSpace(dto.ObjetivoTerapeutico))
            {
                tratamento.ObjetivoTerapeutico = dto.ObjetivoTerapeutico.Trim();
            }

            if (dto.ObservacoesGerais != null)
            {
                tratamento.ObservacoesGerais = dto.ObservacoesGerais.Trim();
            }

            var statusAnterior = tratamento.Status;

            if (novoStatus != null)
            {
                tratamento.Status = novoStatus;

                // Encerrar o tratamento sem data explícita registra o encerramento agora.
                if (novoStatus != StatusTratamento.EmAndamento && tratamento.DataFim == null)
                {
                    tratamento.DataFim = dto.DataFim.HasValue
                        ? AgendarSessaoUseCase.NormalizarParaUtc(dto.DataFim.Value)
                        : DateTime.UtcNow;
                }

                if (novoStatus == StatusTratamento.EmAndamento)
                {
                    tratamento.DataFim = null;
                }
            }
            else if (dto.DataFim.HasValue)
            {
                tratamento.DataFim = AgendarSessaoUseCase.NormalizarParaUtc(dto.DataFim.Value);
            }

            if (tratamento.DataFim.HasValue && tratamento.DataFim.Value < tratamento.DataInicio)
            {
                return Resultado<TratamentoDTO>.Invalido("A data de encerramento não pode ser anterior ao início do tratamento.");
            }

            _tratamentos.Atualizar(tratamento);
            await _tratamentos.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Tratamento", tratamento.Id,
                statusAnterior != tratamento.Status
                    ? $"Tratamento de {tratamento.Paciente?.Nome}: {statusAnterior} → {tratamento.Status}"
                    : $"Alteração do tratamento de {tratamento.Paciente?.Nome}");

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, await ContarSessoes(id)),
                "Tratamento atualizado com sucesso.");
        }

        private async Task<ContagemDeSessoes> ContarSessoes(Guid tratamentoId)
        {
            var contagens = await _sessoes.ContarPorTratamentos(new[] { tratamentoId });
            return contagens.GetValueOrDefault(tratamentoId, new ContagemDeSessoes(0, 0));
        }

        /// <summary>Os totais de sessões de todos os tratamentos saem numa única consulta.</summary>
        private async Task<List<TratamentoDTO>> MapearLista(List<Tratamento> tratamentos)
        {
            var contagens = await _sessoes.ContarPorTratamentos(tratamentos.Select(t => t.Id));

            return tratamentos
                .Select(t => MapearParaDTO(t, contagens.GetValueOrDefault(t.Id, new ContagemDeSessoes(0, 0))))
                .ToList();
        }

        public static TratamentoDTO MapearParaDTO(Tratamento tratamento, ContagemDeSessoes sessoes)
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
                TotalSessoes = sessoes.Total,
                SessoesConcluidas = sessoes.Concluidas
            };
        }
    }
}
