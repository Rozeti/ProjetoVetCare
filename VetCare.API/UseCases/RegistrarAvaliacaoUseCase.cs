using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-007: registro e correção da avaliação clínica.</summary>
    public class RegistrarAvaliacaoUseCase
    {
        public const string TipoRegistro = "AvaliacaoClinica";

        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly ITratamentoRepository _tratamentos;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IObservacaoInternaRepository _observacoes;
        private readonly IVersaoRegistroRepository _versoes;
        private readonly NotificacaoService _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public RegistrarAvaliacaoUseCase(
            IAvaliacaoRepository avaliacoes,
            ITratamentoRepository tratamentos,
            IProntuarioRepository prontuarios,
            IObservacaoInternaRepository observacoes,
            IVersaoRegistroRepository versoes,
            NotificacaoService notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _avaliacoes = avaliacoes;
            _tratamentos = tratamentos;
            _prontuarios = prontuarios;
            _observacoes = observacoes;
            _versoes = versoes;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<AvaliacaoDTO>> Executar(CriarAvaliacaoDTO dto)
        {
            // RN-010: a avaliação só é concluída com os cinco campos clínicos preenchidos.
            var camposPendentes = ListarCamposPendentes(
                dto.QueixaPrincipal, dto.Anamnese, dto.ExameFisico, dto.HipoteseDiagnostica, dto.PlanoTerapeutico);

            if (camposPendentes.Count > 0)
            {
                return Resultado<AvaliacaoDTO>.Invalido(
                    $"Preencha os campos obrigatórios da avaliação: {string.Join(", ", camposPendentes)}.");
            }

            var tratamento = await _tratamentos.ObterPorIdComRelacionamentos(dto.TratamentoId);

            if (tratamento == null)
            {
                return Resultado<AvaliacaoDTO>.NaoEncontrado("Tratamento não encontrado.");
            }

            if (tratamento.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<AvaliacaoDTO>.NaoAutorizado("Este tratamento pertence a outra clínica.");
            }

            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? tratamento.VeterinarioId;

            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<AvaliacaoDTO>.NaoAutorizado("A avaliação deve ser registrada em seu próprio nome.");
            }

            // HU-007, CA-3: o registro entra na linha do tempo do prontuário do paciente.
            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(tratamento.PacienteId);

            var avaliacao = new AvaliacaoClinica
            {
                TratamentoId = dto.TratamentoId,
                ProntuarioId = prontuario.Id,
                VeterinarioId = veterinarioId,
                QueixaPrincipal = dto.QueixaPrincipal.Trim(),
                Anamnese = dto.Anamnese.Trim(),
                ExameFisico = dto.ExameFisico.Trim(),
                HipoteseDiagnostica = dto.HipoteseDiagnostica.Trim(),
                PlanoTerapeutico = dto.PlanoTerapeutico.Trim()
            };

            await _avaliacoes.Adicionar(avaliacao);
            await _avaliacoes.SalvarAlteracoes();

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

            var usuarioTutor = tratamento.Paciente?.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value,
                    tratamento.Paciente?.Nome ?? "seu pet",
                    "Nova avaliação clínica",
                    tratamento.PacienteId);
            }

            return Resultado<AvaliacaoDTO>.Ok(MapearParaDTO(avaliacao), "Avaliação clínica registrada com sucesso.");
        }

        /// <summary>
        /// HU-007, CA-4: correção de uma avaliação já confirmada. A versão anterior é
        /// arquivada antes da alteração, atendendo à RN-004.
        /// </summary>
        public async Task<Resultado<AvaliacaoDTO>> Atualizar(Guid id, AtualizarAvaliacaoDTO dto)
        {
            var camposPendentes = ListarCamposPendentes(
                dto.QueixaPrincipal, dto.Anamnese, dto.ExameFisico, dto.HipoteseDiagnostica, dto.PlanoTerapeutico);

            if (camposPendentes.Count > 0)
            {
                return Resultado<AvaliacaoDTO>.Invalido(
                    $"Preencha os campos obrigatórios da avaliação: {string.Join(", ", camposPendentes)}.");
            }

            var avaliacao = await _avaliacoes.ObterPorId(id);

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
                avaliacao.QueixaPrincipal,
                avaliacao.Anamnese,
                avaliacao.ExameFisico,
                avaliacao.HipoteseDiagnostica,
                avaliacao.PlanoTerapeutico,
                avaliacao.DataRegistro,
                avaliacao.DataUltimaEdicao
            }, _usuarioAtual.Id);

            avaliacao.QueixaPrincipal = dto.QueixaPrincipal.Trim();
            avaliacao.Anamnese = dto.Anamnese.Trim();
            avaliacao.ExameFisico = dto.ExameFisico.Trim();
            avaliacao.HipoteseDiagnostica = dto.HipoteseDiagnostica.Trim();
            avaliacao.PlanoTerapeutico = dto.PlanoTerapeutico.Trim();
            avaliacao.DataUltimaEdicao = DateTime.UtcNow;

            _avaliacoes.Atualizar(avaliacao);
            await _avaliacoes.SalvarAlteracoes();

            return Resultado<AvaliacaoDTO>.Ok(
                MapearParaDTO(avaliacao),
                "Avaliação atualizada. A versão anterior foi preservada no histórico.");
        }

        public async Task<Resultado<AvaliacaoDTO>> ObterPorId(Guid id)
        {
            var avaliacao = await _avaliacoes.ObterPorId(id);

            return avaliacao == null
                ? Resultado<AvaliacaoDTO>.NaoEncontrado("Avaliação não encontrada.")
                : Resultado<AvaliacaoDTO>.Ok(MapearParaDTO(avaliacao));
        }

        public async Task<Resultado<List<HistoricoVersaoDTO>>> ObterHistorico(Guid id)
        {
            var versoes = await _versoes.ObterHistorico(TipoRegistro, id);

            var dtos = versoes.Select(v => new HistoricoVersaoDTO
            {
                Id = v.Id,
                TipoRegistro = v.TipoRegistro,
                RegistroId = v.RegistroId,
                ConteudoAnterior = v.ConteudoAnterior,
                AlteradoPor = v.AlteradoPor?.Nome ?? string.Empty,
                DataAlteracao = v.DataAlteracao
            }).ToList();

            return Resultado<List<HistoricoVersaoDTO>>.Ok(dtos);
        }

        private async Task AtualizarProntuario(Prontuario prontuario)
        {
            prontuario.UltimaAtualizacao = DateTime.UtcNow;
            _prontuarios.Atualizar(prontuario);
            await _prontuarios.SalvarAlteracoes();
        }

        private static List<string> ListarCamposPendentes(
            string queixa, string anamnese, string exame, string hipotese, string plano)
        {
            var pendentes = new List<string>();

            if (string.IsNullOrWhiteSpace(queixa)) pendentes.Add("queixa principal");
            if (string.IsNullOrWhiteSpace(anamnese)) pendentes.Add("anamnese");
            if (string.IsNullOrWhiteSpace(exame)) pendentes.Add("exame físico");
            if (string.IsNullOrWhiteSpace(hipotese)) pendentes.Add("hipótese diagnóstica");
            if (string.IsNullOrWhiteSpace(plano)) pendentes.Add("plano terapêutico");

            return pendentes;
        }

        private static AvaliacaoDTO MapearParaDTO(AvaliacaoClinica avaliacao)
        {
            return new AvaliacaoDTO
            {
                Id = avaliacao.Id,
                TratamentoId = avaliacao.TratamentoId,
                ProntuarioId = avaliacao.ProntuarioId,
                DataRegistro = avaliacao.DataRegistro,
                DataUltimaEdicao = avaliacao.DataUltimaEdicao,
                NomeVeterinario = avaliacao.Veterinario?.Usuario?.Nome ?? string.Empty,
                QueixaPrincipal = avaliacao.QueixaPrincipal,
                Anamnese = avaliacao.Anamnese,
                ExameFisico = avaliacao.ExameFisico,
                HipoteseDiagnostica = avaliacao.HipoteseDiagnostica,
                PlanoTerapeutico = avaliacao.PlanoTerapeutico
            };
        }
    }
}
