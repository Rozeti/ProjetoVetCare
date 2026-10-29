using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-007: registro e correção da avaliação clínica, o primeiro contato veterinário de uma
    /// consulta. A avaliação pertence ao animal; um tratamento, uma receita ou um pedido de exame
    /// podem nascer dela depois, ou nenhum deles.
    /// </summary>
    public class RegistrarAvaliacaoUseCase
    {
        public const string TipoRegistro = "AvaliacaoClinica";

        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IObservacaoInternaRepository _observacoes;
        private readonly IVersaoRegistroRepository _versoes;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public RegistrarAvaliacaoUseCase(
            IAvaliacaoRepository avaliacoes,
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            IProntuarioRepository prontuarios,
            IObservacaoInternaRepository observacoes,
            IVersaoRegistroRepository versoes,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _avaliacoes = avaliacoes;
            _pets = pets;
            _veterinarios = veterinarios;
            _prontuarios = prontuarios;
            _observacoes = observacoes;
            _versoes = versoes;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<AvaliacaoDTO>> Executar(CriarAvaliacaoDTO dto)
        {
            // RN-010: a avaliação só é concluída com motivo, observações e possível diagnóstico.
            var camposPendentes = ListarCamposPendentes(dto.MotivoConsulta, dto.Observacoes, dto.DiagnosticoPossivel);

            if (camposPendentes.Count > 0)
            {
                return Resultado<AvaliacaoDTO>.Invalido(
                    $"Preencha os campos obrigatórios da avaliação: {string.Join(", ", camposPendentes)}.");
            }

            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<AvaliacaoDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<AvaliacaoDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (!pet.EmAcompanhamento)
            {
                return Resultado<AvaliacaoDTO>.Conflito(
                    "O paciente está inativo ou tem óbito registrado; o prontuário fica apenas para consulta.");
            }

            var veterinarioId = dto.VeterinarioId
                                ?? _usuarioAtual.VeterinarioId
                                ?? pet.VeterinarioResponsavelId
                                ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<AvaliacaoDTO>.Invalido("Informe o veterinário responsável pela avaliação.");
            }

            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<AvaliacaoDTO>.NaoAutorizado("A avaliação deve ser registrada em seu próprio nome.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<AvaliacaoDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            // O primeiro contato clínico define quem acompanha um paciente ainda sem responsável.
            if (!pet.VeterinarioResponsavelId.HasValue)
            {
                pet.VeterinarioResponsavelId = veterinario.Id;
                pet.VeterinarioResponsavel = veterinario;
                _pets.Atualizar(pet);
            }

            // HU-007, CA-3: o registro entra na linha do tempo do prontuário do paciente.
            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pet.Id);

            var avaliacao = new AvaliacaoClinica
            {
                PacienteId = pet.Id,
                ProntuarioId = prontuario.Id,
                VeterinarioId = veterinarioId,
                MotivoConsulta = dto.MotivoConsulta.Trim(),
                Observacoes = dto.Observacoes.Trim(),
                DiagnosticoPossivel = dto.DiagnosticoPossivel.Trim()
            };

            await _avaliacoes.Adicionar(avaliacao);
            await _avaliacoes.SalvarAlteracoes();

            avaliacao.Veterinario = veterinario;

            // HU-009: observação interna opcional gravada junto e marcada como restrita.
            if (!string.IsNullOrWhiteSpace(dto.ObservacaoInterna))
            {
                await _observacoes.Adicionar(new ObservacaoInterna
                {
                    ProntuarioId = prontuario.Id,
                    AvaliacaoId = avaliacao.Id,
                    AutorId = _usuarioAtual.Id,
                    Conteudo = dto.ObservacaoInterna.Trim()
                });

                await _observacoes.SalvarAlteracoes();
            }

            await AtualizarProntuario(prontuario);

            // Quem escreveu o diagnóstico fica na trilha, não só quem o consultou.
            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, TipoRegistro, avaliacao.Id,
                $"Avaliação clínica de {pet.Nome}: {avaliacao.MotivoConsulta}");

            var usuarioTutor = pet.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value, pet.Nome, "Nova avaliação clínica", pet.Id);
            }

            return Resultado<AvaliacaoDTO>.Ok(MapearParaDTO(avaliacao), "Avaliação clínica registrada com sucesso.");
        }

        /// <summary>
        /// HU-007, CA-4: correção de uma avaliação já confirmada. A versão anterior é
        /// arquivada antes da alteração, atendendo à RN-004.
        /// </summary>
        public async Task<Resultado<AvaliacaoDTO>> Atualizar(Guid id, AtualizarAvaliacaoDTO dto)
        {
            var camposPendentes = ListarCamposPendentes(dto.MotivoConsulta, dto.Observacoes, dto.DiagnosticoPossivel);

            if (camposPendentes.Count > 0)
            {
                return Resultado<AvaliacaoDTO>.Invalido(
                    $"Preencha os campos obrigatórios da avaliação: {string.Join(", ", camposPendentes)}.");
            }

            var avaliacao = await ObterDaClinica(id);

            if (avaliacao == null)
            {
                return Resultado<AvaliacaoDTO>.NaoEncontrado("Avaliação não encontrada.");
            }

            if (_usuarioAtual.EhVeterinario && avaliacao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado<AvaliacaoDTO>.NaoAutorizado("Você só pode corrigir avaliações registradas por você.");
            }

            await _versoes.Arquivar(TipoRegistro, avaliacao.Id, new
            {
                avaliacao.MotivoConsulta,
                avaliacao.Observacoes,
                avaliacao.DiagnosticoPossivel,
                avaliacao.DataRegistro,
                avaliacao.DataUltimaEdicao
            }, _usuarioAtual.Id);

            avaliacao.MotivoConsulta = dto.MotivoConsulta.Trim();
            avaliacao.Observacoes = dto.Observacoes.Trim();
            avaliacao.DiagnosticoPossivel = dto.DiagnosticoPossivel.Trim();
            avaliacao.DataUltimaEdicao = DateTime.UtcNow;

            _avaliacoes.Atualizar(avaliacao);
            await _avaliacoes.SalvarAlteracoes();

            // A correção também conta como movimentação do prontuário.
            var prontuario = await _prontuarios.ObterPorId(avaliacao.ProntuarioId);

            if (prontuario != null)
            {
                await AtualizarProntuario(prontuario);
            }

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, TipoRegistro, avaliacao.Id,
                $"Correção da avaliação clínica de {avaliacao.Paciente?.Nome}");

            return Resultado<AvaliacaoDTO>.Ok(
                MapearParaDTO(avaliacao),
                "Avaliação atualizada. A versão anterior foi preservada no histórico.");
        }

        public async Task<Resultado<AvaliacaoDTO>> ObterPorId(Guid id)
        {
            var avaliacao = await ObterDaClinica(id);

            return avaliacao == null
                ? Resultado<AvaliacaoDTO>.NaoEncontrado("Avaliação não encontrada.")
                : Resultado<AvaliacaoDTO>.Ok(MapearParaDTO(avaliacao));
        }

        /// <summary>Avaliações do animal, cada uma com os tratamentos que originou.</summary>
        public async Task<Resultado<List<AvaliacaoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<AvaliacaoDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<List<AvaliacaoDTO>>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var avaliacoes = await _avaliacoes.ObterPorPaciente(pacienteId);

            return Resultado<List<AvaliacaoDTO>>.Ok(avaliacoes.Select(a => MapearParaDTO(a)).ToList());
        }

        public async Task<Resultado<List<HistoricoVersaoDTO>>> ObterHistorico(Guid id)
        {
            if (await ObterDaClinica(id) == null)
            {
                return Resultado<List<HistoricoVersaoDTO>>.NaoEncontrado("Avaliação não encontrada.");
            }

            var versoes = await _versoes.ObterHistorico(TipoRegistro, id);

            return Resultado<List<HistoricoVersaoDTO>>.Ok(HistoricoVersaoDTO.MapearLista(versoes));
        }

        /// <summary>
        /// Qualquer leitura ou edição passa por aqui: registro de outra clínica, ou de um
        /// paciente fora da responsabilidade de quem pede, não existe para quem pede.
        /// </summary>
        private async Task<AvaliacaoClinica?> ObterDaClinica(Guid id)
        {
            var avaliacao = await _avaliacoes.ObterPorId(id);
            var paciente = avaliacao?.Paciente;

            return paciente?.ClinicaId == _usuarioAtual.ClinicaId && AcessoAoPaciente.Permitido(_usuarioAtual, paciente)
                ? avaliacao
                : null;
        }

        private async Task AtualizarProntuario(Prontuario prontuario)
        {
            prontuario.UltimaAtualizacao = DateTime.UtcNow;
            _prontuarios.Atualizar(prontuario);
            await _prontuarios.SalvarAlteracoes();
        }

        private static List<string> ListarCamposPendentes(string? motivo, string? observacoes, string? diagnostico)
        {
            var pendentes = new List<string>();

            if (string.IsNullOrWhiteSpace(motivo)) pendentes.Add("motivo da consulta");
            if (string.IsNullOrWhiteSpace(observacoes)) pendentes.Add("observações");
            if (string.IsNullOrWhiteSpace(diagnostico)) pendentes.Add("possível diagnóstico");

            return pendentes;
        }

        /// <summary>
        /// Os tratamentos vêm da própria avaliação quando carregados; quem já tem a lista do
        /// paciente (o prontuário) a informa, junto com as contagens de receitas e exames.
        /// </summary>
        public static AvaliacaoDTO MapearParaDTO(
            AvaliacaoClinica avaliacao,
            IEnumerable<Tratamento>? tratamentos = null,
            int quantidadeReceitas = 0,
            int quantidadePedidosExame = 0)
        {
            var originados = (tratamentos ?? avaliacao.Tratamentos ?? Enumerable.Empty<Tratamento>())
                .Where(t => t.AvaliacaoId == avaliacao.Id)
                .OrderBy(t => t.DataInicio)
                .Select(t => new TratamentoResumoDTO { Id = t.Id, Nome = t.Nome, Status = t.Status })
                .ToList();

            return new AvaliacaoDTO
            {
                Id = avaliacao.Id,
                PacienteId = avaliacao.PacienteId,
                ProntuarioId = avaliacao.ProntuarioId,
                DataRegistro = avaliacao.DataRegistro,
                DataUltimaEdicao = avaliacao.DataUltimaEdicao,
                VeterinarioId = avaliacao.VeterinarioId,
                NomeVeterinario = avaliacao.Veterinario?.Usuario?.Nome ?? string.Empty,
                MotivoConsulta = avaliacao.MotivoConsulta,
                Observacoes = avaliacao.Observacoes,
                DiagnosticoPossivel = avaliacao.DiagnosticoPossivel,
                Tratamentos = originados,
                QuantidadeReceitas = quantidadeReceitas,
                QuantidadePedidosExame = quantidadePedidosExame
            };
        }
    }
}
