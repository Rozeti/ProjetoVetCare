using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-009: observações internas do prontuário. O acesso é restrito a Administrador e
    /// Veterinário (RN-003); o Tutor nunca alcança este caso de uso, nem para leitura.
    /// </summary>
    public class RegistrarObservacaoInternaUseCase
    {
        private readonly IObservacaoInternaRepository _observacoes;
        private readonly IProntuarioRepository _prontuarios;
        private readonly UsuarioAtual _usuarioAtual;

        public RegistrarObservacaoInternaUseCase(
            IObservacaoInternaRepository observacoes,
            IProntuarioRepository prontuarios,
            UsuarioAtual usuarioAtual)
        {
            _observacoes = observacoes;
            _prontuarios = prontuarios;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<ObservacaoInternaDTO>> Executar(CriarObservacaoInternaDTO dto)
        {
            if (!_usuarioAtual.PodeVerObservacoesInternas)
            {
                return Resultado<ObservacaoInternaDTO>.NaoAutorizado(
                    "Apenas Administrador e Veterinário podem registrar observações internas.");
            }

            if (string.IsNullOrWhiteSpace(dto.Conteudo))
            {
                return Resultado<ObservacaoInternaDTO>.Invalido("O conteúdo da observação é obrigatório.");
            }

            var prontuario = await ResolverProntuario(dto);

            if (prontuario == null)
            {
                return Resultado<ObservacaoInternaDTO>.NaoEncontrado(
                    "Informe um prontuário ou um paciente válido para vincular a observação.");
            }

            if (prontuario.Paciente != null && prontuario.Paciente.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<ObservacaoInternaDTO>.NaoAutorizado("Este prontuário pertence a outra clínica.");
            }

            var observacao = new ObservacaoInterna
            {
                ProntuarioId = prontuario.Id,
                AvaliacaoId = dto.AvaliacaoId,
                AtendimentoId = dto.AtendimentoId,
                AutorId = _usuarioAtual.Id,
                Conteudo = dto.Conteudo.Trim()
            };

            await _observacoes.Adicionar(observacao);
            await _observacoes.SalvarAlteracoes();

            var salva = await _observacoes.ObterPorId(observacao.Id);

            return Resultado<ObservacaoInternaDTO>.Ok(
                MapearParaDTO(salva ?? observacao),
                "Observação interna registrada. Ela não será exibida ao tutor.");
        }

        public async Task<Resultado<List<ObservacaoInternaDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            // RN-003: barreira explícita, além da restrição por rota no controller.
            if (!_usuarioAtual.PodeVerObservacoesInternas)
            {
                return Resultado<List<ObservacaoInternaDTO>>.NaoAutorizado(
                    "Observações internas são restritas aos perfis Administrador e Veterinário.");
            }

            var prontuario = await _prontuarios.ObterPorPacienteId(pacienteId);

            if (prontuario == null)
            {
                return Resultado<List<ObservacaoInternaDTO>>.Ok(new List<ObservacaoInternaDTO>());
            }

            var observacoes = await _observacoes.ObterPorProntuario(prontuario.Id);

            return Resultado<List<ObservacaoInternaDTO>>.Ok(observacoes.Select(MapearParaDTO).ToList());
        }

        private async Task<Prontuario?> ResolverProntuario(CriarObservacaoInternaDTO dto)
        {
            if (dto.ProntuarioId.HasValue && dto.ProntuarioId.Value != Guid.Empty)
            {
                return await _prontuarios.ObterPorId(dto.ProntuarioId.Value);
            }

            if (dto.PacienteId.HasValue && dto.PacienteId.Value != Guid.Empty)
            {
                return await _prontuarios.ObterPorPacienteId(dto.PacienteId.Value)
                       ?? await _prontuarios.ObterOuCriarPorPacienteId(dto.PacienteId.Value);
            }

            return null;
        }

        public static ObservacaoInternaDTO MapearParaDTO(ObservacaoInterna observacao)
        {
            return new ObservacaoInternaDTO
            {
                Id = observacao.Id,
                ProntuarioId = observacao.ProntuarioId,
                AvaliacaoId = observacao.AvaliacaoId,
                AtendimentoId = observacao.AtendimentoId,
                Autor = observacao.Autor?.Nome ?? string.Empty,
                Conteudo = observacao.Conteudo,
                DataRegistro = observacao.DataRegistro
            };
        }
    }
}
