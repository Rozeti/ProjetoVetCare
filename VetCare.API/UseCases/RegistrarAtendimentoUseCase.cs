using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-008: evolução, o registro de cada sessão realizada dentro de um tratamento. A evolução
    /// recebe o número da sessão no tratamento, diz o que foi feito e como o animal reagiu, e
    /// pode gerar uma receita.
    /// </summary>
    public class RegistrarAtendimentoUseCase
    {
        public const string TipoRegistro = "AtendimentoFisioterapeutico";

        /// <summary>Observação da sessão criada no registro de uma evolução sem agendamento.</summary>
        public const string SessaoSemAgendamento = "Sessão registrada no prontuário, sem agendamento prévio.";

        private readonly IAtendimentoRepository _atendimentos;
        private readonly ITratamentoRepository _tratamentos;
        private readonly ISessaoRepository _sessoes;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IPetRepository _pets;
        private readonly IMidiaRepository _midias;
        private readonly IObservacaoInternaRepository _observacoes;
        private readonly IVersaoRegistroRepository _versoes;
        private readonly NotificacaoService _notificacoes;
        private readonly AssinadorDeArquivos _assinador;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public RegistrarAtendimentoUseCase(
            IAtendimentoRepository atendimentos,
            ITratamentoRepository tratamentos,
            ISessaoRepository sessoes,
            IProntuarioRepository prontuarios,
            IPetRepository pets,
            IMidiaRepository midias,
            IObservacaoInternaRepository observacoes,
            IVersaoRegistroRepository versoes,
            NotificacaoService notificacoes,
            AssinadorDeArquivos assinador,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _atendimentos = atendimentos;
            _tratamentos = tratamentos;
            _sessoes = sessoes;
            _prontuarios = prontuarios;
            _pets = pets;
            _midias = midias;
            _observacoes = observacoes;
            _versoes = versoes;
            _notificacoes = notificacoes;
            _assinador = assinador;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<AtendimentoDTO>> Executar(CriarAtendimentoDTO dto)
        {
            // HU-008, CA-2: a escala de dor precisa estar entre 0 e 10.
            if (dto.EscalaDor < 0 || dto.EscalaDor > 10)
            {
                return Resultado<AtendimentoDTO>.Invalido("A escala de dor deve ser um valor entre 0 e 10.");
            }

            var pendente = ValidarCampos(dto.TecnicasAplicadas, dto.EvolucaoClinica, dto.ReacaoAnimal);

            if (pendente != null)
            {
                return Resultado<AtendimentoDTO>.Invalido(pendente);
            }

            Sessao? sessao = null;
            Tratamento? tratamento;

            if (dto.SessaoId.HasValue)
            {
                sessao = await _sessoes.ObterPorIdComRelacionamentos(dto.SessaoId.Value);

                if (sessao == null || sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<AtendimentoDTO>.NaoEncontrado("Sessão não encontrada.");
                }

                tratamento = sessao.Tratamento!;
            }
            else if (dto.TratamentoId.HasValue)
            {
                tratamento = await _tratamentos.ObterPorIdComRelacionamentos(dto.TratamentoId.Value);

                if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<AtendimentoDTO>.NaoEncontrado("Tratamento não encontrado.");
                }
            }
            else
            {
                return Resultado<AtendimentoDTO>.Invalido(
                    "Informe a sessão agendada que foi realizada ou o tratamento da sessão.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, tratamento.Paciente))
            {
                return Resultado<AtendimentoDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (sessao?.Status == StatusSessao.Cancelada)
            {
                return Resultado<AtendimentoDTO>.Invalido(
                    "Não é possível registrar uma sessão cancelada.");
            }

            // Concluído e inativado não recebem novas evoluções; o histórico fica como está.
            if (!StatusTratamento.Ativo(tratamento.Status))
            {
                return Resultado<AtendimentoDTO>.Conflito(
                    $"O tratamento \"{tratamento.Nome}\" está {tratamento.Status.ToLowerInvariant()}. " +
                    "Reative-o para registrar novas sessões.");
            }

            // Uma sessão gera no máximo um atendimento (cardinalidade 1..0..1 do diagrama de classes).
            if (sessao != null && await _atendimentos.ObterPorSessao(sessao.Id) != null)
            {
                return Resultado<AtendimentoDTO>.Conflito("Esta sessão já foi registrada.");
            }

            // Sem sessão agendada, quem registra é quem atendeu; a administração registra em
            // nome do veterinário do tratamento.
            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? sessao?.VeterinarioId ?? tratamento.VeterinarioId;

            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<AtendimentoDTO>.NaoAutorizado("A sessão deve ser registrada em seu próprio nome.");
            }

            var sessaoCriadaAgora = false;

            if (sessao == null)
            {
                if (tratamento.Paciente?.EmAcompanhamento != true)
                {
                    return Resultado<AtendimentoDTO>.Conflito(
                        "O paciente está inativo ou tem óbito registrado; não é possível registrar novas sessões.");
                }

                // A sessão realizada sem agendamento entra na agenda de quem atendeu, já concluída.
                sessao = new Sessao
                {
                    TratamentoId = tratamento.Id,
                    VeterinarioId = veterinarioId,
                    DataHora = DateTime.UtcNow,
                    Status = StatusSessao.Concluida,
                    Observacoes = SessaoSemAgendamento,
                    LembreteEnviado = true
                };

                await _sessoes.Adicionar(sessao);
                await _sessoes.SalvarAlteracoes();
                sessaoCriadaAgora = true;
            }

            var pacienteId = tratamento.PacienteId;
            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pacienteId);

            var atendimento = new AtendimentoFisioterapeutico
            {
                SessaoId = sessao.Id,
                TratamentoId = sessao.TratamentoId,
                ProntuarioId = prontuario.Id,
                VeterinarioId = veterinarioId,
                // A sessão seguinte às já registradas no tratamento.
                NumeroSessao = await _atendimentos.ContarPorTratamento(sessao.TratamentoId) + 1,
                TecnicasAplicadas = dto.TecnicasAplicadas.Trim(),
                EscalaDor = dto.EscalaDor,
                EvolucaoClinica = dto.EvolucaoClinica.Trim(),
                ReacaoAnimal = dto.ReacaoAnimal.Trim(),
                Observacoes = (dto.Observacoes ?? string.Empty).Trim(),
                Relatorio = (dto.Relatorio ?? string.Empty).Trim(),
                SinaisVitais = dto.SinaisVitais.Trim(),
                ProximosPassos = dto.ProximosPassos.Trim(),
                PesoKg = dto.PesoKg,
                TemperaturaCelsius = dto.TemperaturaCelsius,
                FrequenciaCardiaca = dto.FrequenciaCardiaca,
                FrequenciaRespiratoria = dto.FrequenciaRespiratoria
            };

            await _atendimentos.Adicionar(atendimento);
            await _atendimentos.SalvarAlteracoes();

            // A primeira evolução de um tratamento planejado marca o início de fato.
            if (tratamento.Status == StatusTratamento.Planejado)
            {
                tratamento.Status = StatusTratamento.EmAndamento;

                if (tratamento.DataInicio > DateTime.UtcNow)
                {
                    tratamento.DataInicio = DateTime.UtcNow;
                }

                _tratamentos.Atualizar(tratamento);
                await _tratamentos.SalvarAlteracoes();
            }

            if (!string.IsNullOrWhiteSpace(dto.ObservacaoInterna))
            {
                await _observacoes.Adicionar(new ObservacaoInterna
                {
                    ProntuarioId = prontuario.Id,
                    AtendimentoId = atendimento.Id,
                    AutorId = _usuarioAtual.Id,
                    Conteudo = dto.ObservacaoInterna.Trim()
                });

                await _observacoes.SalvarAlteracoes();
            }

            // O peso mais recente do atendimento passa a ser o peso atual do paciente.
            if (dto.PesoKg.HasValue)
            {
                await AtualizarPesoDoPaciente(pacienteId, dto.PesoKg.Value);
            }

            // A sessão criada agora já nasce concluída.
            var sessaoConcluidaAgora = sessaoCriadaAgora;

            if (dto.ConcluirSessao && sessao.Status != StatusSessao.Concluida)
            {
                sessao.Status = StatusSessao.Concluida;
                _sessoes.Atualizar(sessao);
                await _sessoes.SalvarAlteracoes();
                sessaoConcluidaAgora = true;
            }

            prontuario.UltimaAtualizacao = DateTime.UtcNow;
            _prontuarios.Atualizar(prontuario);
            await _prontuarios.SalvarAlteracoes();

            // Quem escreveu o registro clínico fica na trilha, não só quem o consultou.
            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, TipoRegistro, atendimento.Id,
                $"Sessão nº {atendimento.NumeroSessao} do tratamento \"{tratamento.Nome}\" de {tratamento.Paciente?.Nome} (dor {atendimento.EscalaDor}/10)");

            // HU-015, CA-3: um único aviso cobre o registro e, quando for o caso, a conclusão da sessão.
            var usuarioTutor = tratamento.Paciente?.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value,
                    tratamento.Paciente?.Nome ?? "seu pet",
                    $"Atendimento da sessão nº {atendimento.NumeroSessao}",
                    pacienteId);
            }

            atendimento.Tratamento = tratamento;

            return Resultado<AtendimentoDTO>.Ok(
                await MapearParaDTO(atendimento),
                sessaoConcluidaAgora
                    ? $"Sessão nº {atendimento.NumeroSessao} registrada e concluída."
                    : $"Sessão nº {atendimento.NumeroSessao} registrada.");
        }

        /// <summary>RN-004: a edição arquiva a versão anterior antes de aplicar as mudanças.</summary>
        public async Task<Resultado<AtendimentoDTO>> Atualizar(Guid id, AtualizarAtendimentoDTO dto)
        {
            if (dto.EscalaDor < 0 || dto.EscalaDor > 10)
            {
                return Resultado<AtendimentoDTO>.Invalido("A escala de dor deve ser um valor entre 0 e 10.");
            }

            var pendente = ValidarCampos(dto.TecnicasAplicadas, dto.EvolucaoClinica, dto.ReacaoAnimal);

            if (pendente != null)
            {
                return Resultado<AtendimentoDTO>.Invalido(pendente);
            }

            var atendimento = await ObterDaClinica(id);

            if (atendimento == null)
            {
                return Resultado<AtendimentoDTO>.NaoEncontrado("Sessão não encontrada.");
            }

            if (_usuarioAtual.EhVeterinario && atendimento.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado<AtendimentoDTO>.NaoAutorizado(
                    "Você só pode corrigir sessões registradas por você.");
            }

            await _versoes.Arquivar(TipoRegistro, atendimento.Id, new
            {
                atendimento.TecnicasAplicadas,
                atendimento.EscalaDor,
                atendimento.EvolucaoClinica,
                atendimento.ReacaoAnimal,
                atendimento.Observacoes,
                atendimento.Relatorio,
                atendimento.SinaisVitais,
                atendimento.ProximosPassos,
                atendimento.PesoKg,
                atendimento.TemperaturaCelsius,
                atendimento.FrequenciaCardiaca,
                atendimento.FrequenciaRespiratoria,
                atendimento.DataRegistro,
                atendimento.DataUltimaEdicao
            }, _usuarioAtual.Id);

            atendimento.TecnicasAplicadas = dto.TecnicasAplicadas.Trim();
            atendimento.EscalaDor = dto.EscalaDor;
            atendimento.EvolucaoClinica = dto.EvolucaoClinica.Trim();
            atendimento.ReacaoAnimal = dto.ReacaoAnimal.Trim();
            atendimento.Observacoes = (dto.Observacoes ?? string.Empty).Trim();
            atendimento.Relatorio = (dto.Relatorio ?? string.Empty).Trim();
            atendimento.SinaisVitais = dto.SinaisVitais.Trim();
            atendimento.ProximosPassos = dto.ProximosPassos.Trim();
            atendimento.PesoKg = dto.PesoKg;
            atendimento.TemperaturaCelsius = dto.TemperaturaCelsius;
            atendimento.FrequenciaCardiaca = dto.FrequenciaCardiaca;
            atendimento.FrequenciaRespiratoria = dto.FrequenciaRespiratoria;
            atendimento.DataUltimaEdicao = DateTime.UtcNow;

            _atendimentos.Atualizar(atendimento);
            await _atendimentos.SalvarAlteracoes();

            // A correção também conta como movimentação do prontuário.
            await MarcarProntuarioAtualizado(atendimento.ProntuarioId);

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, TipoRegistro, atendimento.Id,
                $"Correção da sessão nº {atendimento.NumeroSessao} de {atendimento.Sessao?.Tratamento?.Paciente?.Nome}");

            return Resultado<AtendimentoDTO>.Ok(
                await MapearParaDTO(atendimento),
                "Sessão corrigida. A versão anterior foi preservada no histórico.");
        }

        public async Task<Resultado<AtendimentoDTO>> ObterPorId(Guid id)
        {
            var atendimento = await ObterDaClinica(id);

            return atendimento == null
                ? Resultado<AtendimentoDTO>.NaoEncontrado("Sessão não encontrada.")
                : Resultado<AtendimentoDTO>.Ok(await MapearParaDTO(atendimento));
        }

        /// <summary>
        /// Tela de evoluções: filtra por animal, tratamento, intervalo de datas (dias da clínica),
        /// profissional e número da sessão. Dentro de um tratamento as sessões vêm em ordem
        /// cronológica. Toda a equipe clínica, inclusive qualquer veterinário, alcança as
        /// evoluções de todos os pacientes da clínica.
        /// </summary>
        public async Task<Resultado<PaginaDe<EvolucaoResumoDTO>>> Listar(
            Guid? pacienteId,
            Guid? tratamentoId,
            DateTime? inicio,
            DateTime? fim,
            Guid? veterinarioId,
            int? numeroSessao,
            ParametrosPagina parametros)
        {
            if (inicio.HasValue && fim.HasValue &&
                RelogioDaClinica.Padrao.DiaDaClinica(fim.Value) < RelogioDaClinica.Padrao.DiaDaClinica(inicio.Value))
            {
                return Resultado<PaginaDe<EvolucaoResumoDTO>>.Invalido("A data final não pode ser anterior à inicial.");
            }

            if (numeroSessao is < 1)
            {
                return Resultado<PaginaDe<EvolucaoResumoDTO>>.Invalido("O número da sessão começa em 1.");
            }

            if (pacienteId.HasValue)
            {
                var pet = await _pets.ObterPorId(pacienteId.Value);

                if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<PaginaDe<EvolucaoResumoDTO>>.NaoEncontrado("Paciente não encontrado.");
                }

                if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
                {
                    return Resultado<PaginaDe<EvolucaoResumoDTO>>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
                }
            }

            if (tratamentoId.HasValue)
            {
                var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(tratamentoId.Value);

                if (tratamento == null || tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<PaginaDe<EvolucaoResumoDTO>>.NaoEncontrado("Tratamento não encontrado.");
                }

                if (!AcessoAoPaciente.Permitido(_usuarioAtual, tratamento.Paciente))
                {
                    return Resultado<PaginaDe<EvolucaoResumoDTO>>.NaoAutorizado("Você não tem acesso a este tratamento.");
                }
            }

            var relogio = RelogioDaClinica.Padrao;

            var filtro = new FiltroDeEvolucoes
            {
                PacienteId = pacienteId,
                TratamentoId = tratamentoId,
                VeterinarioId = veterinarioId,
                NumeroSessao = numeroSessao,
                InicioUtc = inicio.HasValue ? relogio.ParaUtc(relogio.DiaDaClinica(inicio.Value)) : null,
                FimUtc = fim.HasValue ? relogio.ParaUtc(relogio.DiaDaClinica(fim.Value).AddDays(1)) : null,
                OrdemCronologica = tratamentoId.HasValue
            };

            var pagina = await _atendimentos.Listar(_usuarioAtual.ClinicaId, filtro, parametros);

            return Resultado<PaginaDe<EvolucaoResumoDTO>>.Ok(pagina.Converter(a => new EvolucaoResumoDTO
            {
                Id = a.Id,
                SessaoId = a.SessaoId,
                TratamentoId = a.TratamentoId,
                NomeTratamento = a.Tratamento?.Nome ?? string.Empty,
                PacienteId = a.Tratamento?.PacienteId ?? Guid.Empty,
                NomePaciente = a.Tratamento?.Paciente?.Nome ?? string.Empty,
                NumeroSessao = a.NumeroSessao,
                DataRegistro = a.DataRegistro,
                VeterinarioId = a.VeterinarioId,
                NomeVeterinario = a.Veterinario?.Usuario?.Nome ?? string.Empty,
                TecnicasAplicadas = a.TecnicasAplicadas,
                EvolucaoClinica = a.EvolucaoClinica,
                ReacaoAnimal = a.ReacaoAnimal,
                EscalaDor = a.EscalaDor,
                Editado = a.DataUltimaEdicao.HasValue
            }));
        }

        public async Task<Resultado<AtendimentoDTO?>> ObterPorSessao(Guid sessaoId)
        {
            var sessao = await _sessoes.ObterPorIdComRelacionamentos(sessaoId);

            if (sessao == null || sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<AtendimentoDTO?>.NaoEncontrado("Sessão não encontrada.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, sessao.Tratamento?.Paciente))
            {
                return Resultado<AtendimentoDTO?>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var atendimento = await _atendimentos.ObterPorSessao(sessaoId);

            if (atendimento == null)
            {
                return Resultado<AtendimentoDTO?>.Ok(null);
            }

            var completo = await _atendimentos.ObterPorId(atendimento.Id);
            return Resultado<AtendimentoDTO?>.Ok(await MapearParaDTO(completo!));
        }

        public async Task<Resultado<List<HistoricoVersaoDTO>>> ObterHistorico(Guid id)
        {
            if (await ObterDaClinica(id) == null)
            {
                return Resultado<List<HistoricoVersaoDTO>>.NaoEncontrado("Sessão não encontrada.");
            }

            var versoes = await _versoes.ObterHistorico(TipoRegistro, id);

            return Resultado<List<HistoricoVersaoDTO>>.Ok(HistoricoVersaoDTO.MapearLista(versoes));
        }

        /// <summary>
        /// Qualquer leitura ou edição passa por aqui: registro de outra clínica, ou de um
        /// paciente fora da responsabilidade de quem pede, não existe para quem pede.
        /// </summary>
        private async Task<AtendimentoFisioterapeutico?> ObterDaClinica(Guid id)
        {
            var atendimento = await _atendimentos.ObterPorId(id);
            var paciente = atendimento?.Sessao?.Tratamento?.Paciente;

            return paciente?.ClinicaId == _usuarioAtual.ClinicaId && AcessoAoPaciente.Permitido(_usuarioAtual, paciente)
                ? atendimento
                : null;
        }

        private async Task MarcarProntuarioAtualizado(Guid prontuarioId)
        {
            var prontuario = await _prontuarios.ObterPorId(prontuarioId);

            if (prontuario == null)
            {
                return;
            }

            prontuario.UltimaAtualizacao = DateTime.UtcNow;
            _prontuarios.Atualizar(prontuario);
            await _prontuarios.SalvarAlteracoes();
        }

        private static string? ValidarCampos(string? procedimentos, string? descricao, string? reacao)
        {
            if (string.IsNullOrWhiteSpace(procedimentos))
                return "Informe os procedimentos realizados na sessão.";

            if (string.IsNullOrWhiteSpace(descricao))
                return "Descreva o que foi realizado na sessão.";

            if (string.IsNullOrWhiteSpace(reacao))
                return "Informe como o animal reagiu à sessão.";

            return null;
        }

        private async Task AtualizarPesoDoPaciente(Guid pacienteId, decimal peso)
        {
            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null)
            {
                return;
            }

            pet.PesoAtualKg = peso;
            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();
        }

        private async Task<AtendimentoDTO> MapearParaDTO(AtendimentoFisioterapeutico atendimento)
        {
            var midias = await _midias.ObterPorSessao(atendimento.SessaoId);

            var tratamento = atendimento.Tratamento ?? atendimento.Sessao?.Tratamento;

            return new AtendimentoDTO
            {
                Id = atendimento.Id,
                SessaoId = atendimento.SessaoId,
                TratamentoId = atendimento.TratamentoId,
                NomeTratamento = tratamento?.Nome ?? string.Empty,
                PacienteId = tratamento?.PacienteId ?? Guid.Empty,
                NomePaciente = tratamento?.Paciente?.Nome ?? atendimento.Sessao?.Tratamento?.Paciente?.Nome ?? string.Empty,
                ProntuarioId = atendimento.ProntuarioId,
                NumeroSessao = atendimento.NumeroSessao,
                DataRegistro = atendimento.DataRegistro,
                DataUltimaEdicao = atendimento.DataUltimaEdicao,
                VeterinarioId = atendimento.VeterinarioId,
                NomeVeterinario = atendimento.Veterinario?.Usuario?.Nome ?? string.Empty,
                TecnicasAplicadas = atendimento.TecnicasAplicadas,
                EscalaDor = atendimento.EscalaDor,
                EvolucaoClinica = atendimento.EvolucaoClinica,
                ReacaoAnimal = atendimento.ReacaoAnimal,
                Observacoes = atendimento.Observacoes,
                Relatorio = atendimento.Relatorio,
                SinaisVitais = atendimento.SinaisVitais,
                ProximosPassos = atendimento.ProximosPassos,
                PesoKg = atendimento.PesoKg,
                TemperaturaCelsius = atendimento.TemperaturaCelsius,
                FrequenciaCardiaca = atendimento.FrequenciaCardiaca,
                FrequenciaRespiratoria = atendimento.FrequenciaRespiratoria,
                Midias = midias.Select(m => AnexarMidiaUseCase.MapearParaDTO(m, _assinador)).ToList()
            };
        }
    }
}
