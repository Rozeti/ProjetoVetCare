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
        private readonly IPetRepository _pets;
        private readonly UsuarioAtual _usuarioAtual;

        public RegistrarObservacaoInternaUseCase(
            IObservacaoInternaRepository observacoes,
            IProntuarioRepository prontuarios,
            IPetRepository pets,
            UsuarioAtual usuarioAtual)
        {
            _observacoes = observacoes;
            _prontuarios = prontuarios;
            _pets = pets;
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

            var prontuario = await ResolverProntuarioDaClinica(dto.ProntuarioId, dto.PacienteId);

            if (prontuario == null)
            {
                return Resultado<ObservacaoInternaDTO>.NaoEncontrado(
                    "Informe um prontuário ou um paciente válido desta clínica para vincular a observação.");
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

            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<ObservacaoInternaDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            var prontuario = await _prontuarios.ObterPorPacienteId(pacienteId);

            if (prontuario == null)
            {
                return Resultado<List<ObservacaoInternaDTO>>.Ok(new List<ObservacaoInternaDTO>());
            }

            var observacoes = await _observacoes.ObterPorProntuario(prontuario.Id);

            return Resultado<List<ObservacaoInternaDTO>>.Ok(observacoes.Select(MapearParaDTO).ToList());
        }

        /// <summary>Só devolve o prontuário quando o paciente é desta clínica; sem paciente carregado, recusa.</summary>
        private async Task<Prontuario?> ResolverProntuarioDaClinica(Guid? prontuarioId, Guid? pacienteId)
        {
            Prontuario? prontuario = null;

            if (prontuarioId.HasValue && prontuarioId.Value != Guid.Empty)
            {
                prontuario = await _prontuarios.ObterPorId(prontuarioId.Value);
            }
            else if (pacienteId.HasValue && pacienteId.Value != Guid.Empty)
            {
                var pet = await _pets.ObterPorId(pacienteId.Value);

                if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return null;
                }

                prontuario = await _prontuarios.ObterPorPacienteId(pet.Id)
                             ?? await _prontuarios.ObterOuCriarPorPacienteId(pet.Id);

                prontuario.Paciente ??= pet;
            }

            if (prontuario?.Paciente == null || prontuario.Paciente.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return null;
            }

            return prontuario;
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
