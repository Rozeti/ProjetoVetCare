using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Tratamento: procedimento ou plano terapêutico encaminhado a partir de uma avaliação
    /// específica. Não existe sem a avaliação de origem; cada sessão realizada gera uma evolução
    /// vinculada a ele. Concluído é definitivo; inativado preserva o histórico e pode ser
    /// reativado.
    /// </summary>
    public class GerenciarTratamentosUseCase
    {
        private readonly ITratamentoRepository _tratamentos;
        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IProntuarioRepository _prontuarios;
        private readonly ISessaoRepository _sessoes;
        private readonly IAtendimentoRepository _atendimentos;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarTratamentosUseCase(
            ITratamentoRepository tratamentos,
            IAvaliacaoRepository avaliacoes,
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            IProntuarioRepository prontuarios,
            ISessaoRepository sessoes,
            IAtendimentoRepository atendimentos,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _tratamentos = tratamentos;
            _avaliacoes = avaliacoes;
            _pets = pets;
            _veterinarios = veterinarios;
            _prontuarios = prontuarios;
            _sessoes = sessoes;
            _atendimentos = atendimentos;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<TratamentoDTO>> Cadastrar(CriarTratamentoDTO dto)
        {
            var camposInvalidos = ValidarCampos(dto.Nome, dto.Tipo, dto.Descricao, dto.DuracaoPrevista, dto.Frequencia, dto.SessoesPrevistas);

            if (camposInvalidos != null)
            {
                return Resultado<TratamentoDTO>.Invalido(camposInvalidos);
            }

            // O tratamento nasce de uma avaliação: é ela que diz de qual animal ele é.
            var avaliacao = await _avaliacoes.ObterPorId(dto.AvaliacaoId);

            if (avaliacao == null || avaliacao.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Avaliação de origem não encontrada.");
            }

            var pet = await _pets.ObterPorIdComTutor(avaliacao.PacienteId);

            if (pet == null)
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

            var status = string.IsNullOrWhiteSpace(dto.Status)
                ? StatusTratamento.Planejado
                : StatusTratamento.Normalizar(dto.Status);

            if (status is not (StatusTratamento.Planejado or StatusTratamento.EmAndamento))
            {
                return Resultado<TratamentoDTO>.Invalido(
                    $"Um tratamento começa como {StatusTratamento.Planejado} ou {StatusTratamento.EmAndamento}.");
            }

            // Qualquer veterinário da clínica pode conduzir o tratamento: o informado, o próprio
            // veterinário que o cadastra ou, sem nenhum dos dois, o responsável pelo paciente. Sem
            // responsável designado ainda (cadastro vindo do aplicativo), o tratamento define quem é.
            var veterinarioId = dto.VeterinarioId
                                ?? _usuarioAtual.VeterinarioId
                                ?? pet.VeterinarioResponsavelId
                                ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<TratamentoDTO>.Invalido("Informe o veterinário responsável pelo tratamento.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<TratamentoDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            // Sem data o tratamento começa hoje. Só o planejado pode começar no futuro.
            var dataInicio = dto.DataInicio is { } informada && informada != default
                ? AgendarSessaoUseCase.NormalizarParaUtc(informada)
                : DateTime.UtcNow;

            if (status == StatusTratamento.EmAndamento &&
                RelogioDaClinica.Padrao.DiaDaClinica(dataInicio) > RelogioDaClinica.Padrao.Hoje)
            {
                return Resultado<TratamentoDTO>.Invalido(
                    "Um tratamento em andamento não pode ter início no futuro. Cadastre-o como planejado.");
            }

            if (!pet.VeterinarioResponsavelId.HasValue)
            {
                pet.VeterinarioResponsavelId = veterinario.Id;
                pet.VeterinarioResponsavel = veterinario;
                _pets.Atualizar(pet);
            }

            var tratamento = new Tratamento
            {
                PacienteId = pet.Id,
                AvaliacaoId = avaliacao.Id,
                VeterinarioId = veterinarioId,
                Nome = dto.Nome.Trim(),
                Tipo = dto.Tipo.Trim(),
                Descricao = dto.Descricao.Trim(),
                DuracaoPrevista = dto.DuracaoPrevista.Trim(),
                Frequencia = dto.Frequencia.Trim(),
                SessoesPrevistas = dto.SessoesPrevistas,
                DataInicio = dataInicio,
                ObservacoesGerais = (dto.ObservacoesGerais ?? string.Empty).Trim(),
                Status = status
            };

            await _tratamentos.Adicionar(tratamento);
            await _tratamentos.SalvarAlteracoes();

            // Garante o prontuário do paciente já na abertura do tratamento.
            await _prontuarios.ObterOuCriarPorPacienteId(pet.Id);

            tratamento.Paciente = pet;
            tratamento.Veterinario = veterinario;
            tratamento.Avaliacao = avaliacao;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Tratamento", tratamento.Id,
                $"Tratamento \"{tratamento.Nome}\" de {pet.Nome} com {veterinario.Usuario?.Nome}, a partir da avaliação de " +
                RelogioDaClinica.Padrao.Formatar(avaliacao.DataRegistro, "dd/MM/yyyy"));

            var usuarioTutor = pet.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value, pet.Nome, $"Novo tratamento: {tratamento.Nome}", pet.Id);
            }

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, new ContagemDeSessoes(0, 0), 0),
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

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, await ContarSessoes(id), await _atendimentos.ContarPorTratamento(id)));
        }

        /// <summary>
        /// Campos omitidos (nulos) mantêm o valor atual. Inativar não apaga nada: as sessões e
        /// evoluções continuam no histórico, e o tratamento pode ser reativado depois.
        /// </summary>
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

                // Concluído é definitivo: o histórico do tratamento se encerra ali.
                if (tratamento.Status == StatusTratamento.Concluido && novoStatus != StatusTratamento.Concluido)
                {
                    return Resultado<TratamentoDTO>.Conflito(
                        "Um tratamento concluído não pode ser reaberto. Registre uma nova avaliação e inicie um novo tratamento.");
                }

                if (StatusTratamento.Ativo(novoStatus) && !StatusTratamento.Ativo(tratamento.Status)
                    && !tratamento.Paciente!.EmAcompanhamento)
                {
                    return Resultado<TratamentoDTO>.Conflito(
                        "O paciente está inativo ou tem óbito registrado; o tratamento não pode ser reativado.");
                }
            }

            // Os campos de texto, quando enviados, não podem ficar em branco.
            var camposInvalidos = ValidarCampos(
                dto.Nome ?? tratamento.Nome,
                dto.Tipo ?? tratamento.Tipo,
                dto.Descricao ?? tratamento.Descricao,
                dto.DuracaoPrevista ?? tratamento.DuracaoPrevista,
                dto.Frequencia ?? tratamento.Frequencia,
                dto.SessoesPrevistas ?? tratamento.SessoesPrevistas);

            if (camposInvalidos != null)
            {
                return Resultado<TratamentoDTO>.Invalido(camposInvalidos);
            }

            var anterior = new
            {
                tratamento.Nome, tratamento.Tipo, tratamento.Descricao, tratamento.DuracaoPrevista,
                tratamento.Frequencia, tratamento.SessoesPrevistas, tratamento.Status
            };

            if (dto.Nome != null) tratamento.Nome = dto.Nome.Trim();
            if (dto.Tipo != null) tratamento.Tipo = dto.Tipo.Trim();
            if (dto.Descricao != null) tratamento.Descricao = dto.Descricao.Trim();
            if (dto.DuracaoPrevista != null) tratamento.DuracaoPrevista = dto.DuracaoPrevista.Trim();
            if (dto.Frequencia != null) tratamento.Frequencia = dto.Frequencia.Trim();
            if (dto.SessoesPrevistas.HasValue) tratamento.SessoesPrevistas = dto.SessoesPrevistas.Value;
            if (dto.ObservacoesGerais != null) tratamento.ObservacoesGerais = dto.ObservacoesGerais.Trim();

            if (dto.DataInicio.HasValue && dto.DataInicio.Value != default)
            {
                tratamento.DataInicio = AgendarSessaoUseCase.NormalizarParaUtc(dto.DataInicio.Value);
            }

            var statusFinal = novoStatus ?? tratamento.Status;

            // Em andamento, o início não fica no futuro: começar agora o que estava planejado
            // traz a data para hoje.
            if (statusFinal == StatusTratamento.EmAndamento &&
                RelogioDaClinica.Padrao.DiaDaClinica(tratamento.DataInicio) > RelogioDaClinica.Padrao.Hoje)
            {
                if (dto.DataInicio.HasValue)
                {
                    return Resultado<TratamentoDTO>.Invalido(
                        "Um tratamento em andamento não pode ter início no futuro. Mantenha-o como planejado.");
                }

                tratamento.DataInicio = DateTime.UtcNow;
            }

            if (novoStatus != null)
            {
                tratamento.Status = novoStatus;

                // Encerrar (concluir ou inativar) sem data explícita registra o encerramento agora.
                if (!StatusTratamento.Ativo(novoStatus) && tratamento.DataFim == null)
                {
                    tratamento.DataFim = dto.DataFim.HasValue
                        ? AgendarSessaoUseCase.NormalizarParaUtc(dto.DataFim.Value)
                        : DateTime.UtcNow;
                }

                if (StatusTratamento.Ativo(novoStatus))
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
                anterior.Status != tratamento.Status
                    ? $"Tratamento \"{tratamento.Nome}\" de {tratamento.Paciente?.Nome}: {anterior.Status} → {tratamento.Status}"
                    : $"Alteração do tratamento \"{tratamento.Nome}\" de {tratamento.Paciente?.Nome}");

            var mensagem = novoStatus switch
            {
                StatusTratamento.Inativado when anterior.Status != StatusTratamento.Inativado =>
                    "Tratamento inativado. As sessões registradas continuam no histórico.",
                StatusTratamento.Concluido when anterior.Status != StatusTratamento.Concluido =>
                    "Tratamento concluído.",
                _ when novoStatus != null && StatusTratamento.Ativo(novoStatus) && !StatusTratamento.Ativo(anterior.Status) =>
                    "Tratamento reativado.",
                _ => "Tratamento atualizado com sucesso."
            };

            return Resultado<TratamentoDTO>.Ok(
                MapearParaDTO(tratamento, await ContarSessoes(id), await _atendimentos.ContarPorTratamento(id)),
                mensagem);
        }

        private static string? ValidarCampos(
            string? nome, string? tipo, string? descricao, string? duracao, string? frequencia, int sessoesPrevistas)
        {
            if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length < 3)
                return "Informe o nome do tratamento (mínimo de 3 caracteres).";

            if (string.IsNullOrWhiteSpace(tipo))
                return "Informe o tipo do tratamento.";

            if (string.IsNullOrWhiteSpace(descricao) || descricao.Trim().Length < 3)
                return "Descreva o tratamento (mínimo de 3 caracteres).";

            if (string.IsNullOrWhiteSpace(duracao))
                return "Informe a duração prevista do tratamento.";

            if (string.IsNullOrWhiteSpace(frequencia))
                return "Informe a frequência das sessões.";

            if (sessoesPrevistas < 1 || sessoesPrevistas > 365)
                return "Informe entre 1 e 365 sessões previstas.";

            return null;
        }

        private async Task<ContagemDeSessoes> ContarSessoes(Guid tratamentoId)
        {
            var contagens = await _sessoes.ContarPorTratamentos(new[] { tratamentoId });
            return contagens.GetValueOrDefault(tratamentoId, new ContagemDeSessoes(0, 0));
        }

        /// <summary>Os totais de sessões e de evoluções de todos os tratamentos saem em duas consultas.</summary>
        private async Task<List<TratamentoDTO>> MapearLista(List<Tratamento> tratamentos)
        {
            var ids = tratamentos.Select(t => t.Id).ToList();
            var contagens = await _sessoes.ContarPorTratamentos(ids);
            var evolucoes = await _atendimentos.ContarPorTratamentos(ids);

            return tratamentos
                .Select(t => MapearParaDTO(
                    t,
                    contagens.GetValueOrDefault(t.Id, new ContagemDeSessoes(0, 0)),
                    evolucoes.GetValueOrDefault(t.Id)))
                .ToList();
        }

        public static TratamentoDTO MapearParaDTO(Tratamento tratamento, ContagemDeSessoes sessoes, int evolucoesRegistradas)
        {
            return new TratamentoDTO
            {
                Id = tratamento.Id,
                PacienteId = tratamento.PacienteId,
                NomePaciente = tratamento.Paciente?.Nome ?? string.Empty,
                AvaliacaoId = tratamento.AvaliacaoId,
                DataAvaliacao = tratamento.Avaliacao?.DataRegistro,
                MotivoAvaliacao = tratamento.Avaliacao?.MotivoConsulta ?? string.Empty,
                VeterinarioId = tratamento.VeterinarioId,
                NomeVeterinario = tratamento.Veterinario?.Usuario?.Nome ?? string.Empty,
                Nome = tratamento.Nome,
                Tipo = tratamento.Tipo,
                Descricao = tratamento.Descricao,
                DuracaoPrevista = tratamento.DuracaoPrevista,
                Frequencia = tratamento.Frequencia,
                SessoesPrevistas = tratamento.SessoesPrevistas,
                DataInicio = tratamento.DataInicio,
                DataFim = tratamento.DataFim,
                Status = tratamento.Status,
                ObservacoesGerais = tratamento.ObservacoesGerais,
                TotalSessoes = sessoes.Total,
                SessoesConcluidas = sessoes.Concluidas,
                EvolucoesRegistradas = evolucoesRegistradas
            };
        }
    }
}
