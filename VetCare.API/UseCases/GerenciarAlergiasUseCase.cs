using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Alergias, comorbidades e restrições do paciente. São informações de segurança
    /// clínica: ficam visíveis a todos os perfis, inclusive ao tutor, porque precisam
    /// ser consideradas antes de qualquer conduta.
    /// </summary>
    public class GerenciarAlergiasUseCase
    {
        private static readonly string[] TiposValidos = { "Alergia", "Comorbidade", "Restricao", "Cirurgia" };
        private static readonly string[] GravidadesValidas = { "Leve", "Moderada", "Grave" };

        private readonly IAlergiaRepository _alergias;
        private readonly IPetRepository _pets;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarAlergiasUseCase(IAlergiaRepository alergias, IPetRepository pets, UsuarioAtual usuarioAtual)
        {
            _alergias = alergias;
            _pets = pets;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<AlergiaDTO>>> ListarPorPaciente(Guid pacienteId, bool apenasAtivas)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<AlergiaDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (_usuarioAtual.EhTutor && pet.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<List<AlergiaDTO>>.NaoAutorizado("Você não tem acesso a este paciente.");
            }

            var registros = await _alergias.ObterPorPaciente(pacienteId, apenasAtivas);

            return Resultado<List<AlergiaDTO>>.Ok(registros.Select(MapearParaDTO).ToList());
        }

        public async Task<Resultado<AlergiaDTO>> Registrar(CriarAlergiaDTO dto)
        {
            if (!TiposValidos.Contains(dto.Tipo, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<AlergiaDTO>.Invalido(
                    $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}.");
            }

            if (!GravidadesValidas.Contains(dto.Gravidade, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<AlergiaDTO>.Invalido(
                    $"Gravidade inválida. Use uma destas: {string.Join(", ", GravidadesValidas)}.");
            }

            var pet = await _pets.ObterPorId(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<AlergiaDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            var alergia = new AlergiaCondicao
            {
                PacienteId = dto.PacienteId,
                RegistradoPorId = _usuarioAtual.Id,
                Tipo = Normalizar(dto.Tipo, TiposValidos),
                Descricao = dto.Descricao.Trim(),
                Gravidade = Normalizar(dto.Gravidade, GravidadesValidas)
            };

            await _alergias.Adicionar(alergia);
            await _alergias.SalvarAlteracoes();

            var salva = await _alergias.ObterPorId(alergia.Id);

            return Resultado<AlergiaDTO>.Ok(
                MapearParaDTO(salva ?? alergia),
                "Alerta clínico registrado no prontuário do paciente.");
        }

        /// <summary>
        /// Uma condição resolvida é desativada em vez de excluída: saber que ela
        /// existiu continua sendo informação clínica relevante.
        /// </summary>
        public async Task<Resultado> AlterarStatus(Guid id, bool ativa)
        {
            var alergia = await _alergias.ObterPorId(id);

            if (alergia == null || alergia.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Registro não encontrado.");
            }

            alergia.Ativa = ativa;

            _alergias.Atualizar(alergia);
            await _alergias.SalvarAlteracoes();

            return Resultado.Ok(ativa ? "Alerta reativado." : "Alerta arquivado no histórico do paciente.");
        }

        private static string Normalizar(string valor, string[] validos) =>
            validos.FirstOrDefault(v => string.Equals(v, valor, StringComparison.OrdinalIgnoreCase)) ?? valor;

        public static AlergiaDTO MapearParaDTO(AlergiaCondicao alergia)
        {
            return new AlergiaDTO
            {
                Id = alergia.Id,
                PacienteId = alergia.PacienteId,
                Tipo = alergia.Tipo,
                Descricao = alergia.Descricao,
                Gravidade = alergia.Gravidade,
                RegistradoPor = alergia.RegistradoPor?.Nome ?? string.Empty,
                DataRegistro = alergia.DataRegistro,
                Ativa = alergia.Ativa
            };
        }
    }
}
