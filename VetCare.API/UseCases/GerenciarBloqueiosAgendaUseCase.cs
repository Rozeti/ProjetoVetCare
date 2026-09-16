using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Períodos de indisponibilidade do veterinário. Complementam a RN-002: além de
    /// não haver outra sessão no horário, o profissional precisa estar disponível.
    /// </summary>
    public class GerenciarBloqueiosAgendaUseCase
    {
        private readonly IBloqueioAgendaRepository _bloqueios;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly ISessaoRepository _sessoes;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarBloqueiosAgendaUseCase(
            IBloqueioAgendaRepository bloqueios,
            IVeterinarioRepository veterinarios,
            ISessaoRepository sessoes,
            UsuarioAtual usuarioAtual)
        {
            _bloqueios = bloqueios;
            _veterinarios = veterinarios;
            _sessoes = sessoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<BloqueioAgendaDTO>>> Listar(Guid? veterinarioId, DateTime inicio, DateTime fim)
        {
            var id = veterinarioId ?? _usuarioAtual.VeterinarioId;

            if (id == null)
            {
                return Resultado<List<BloqueioAgendaDTO>>.Invalido("Informe o veterinário.");
            }

            var veterinario = await _veterinarios.ObterPorId(id.Value);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<BloqueioAgendaDTO>>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            var bloqueios = await _bloqueios.ObterPorPeriodo(id.Value, inicio, fim);

            return Resultado<List<BloqueioAgendaDTO>>.Ok(bloqueios.Select(b => MapearParaDTO(b, veterinario)).ToList());
        }

        public async Task<Resultado<BloqueioAgendaDTO>> Criar(CriarBloqueioAgendaDTO dto)
        {
            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<BloqueioAgendaDTO>.Invalido("Informe o veterinário do bloqueio.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<BloqueioAgendaDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<BloqueioAgendaDTO>.NaoAutorizado("Você só pode bloquear a sua própria agenda.");
            }

            var inicio = AgendarSessaoUseCase.NormalizarParaUtc(dto.Inicio);
            var fim = AgendarSessaoUseCase.NormalizarParaUtc(dto.Fim);

            if (fim <= inicio)
            {
                return Resultado<BloqueioAgendaDTO>.Invalido("O fim do bloqueio deve ser posterior ao início.");
            }

            // Bloquear um período que já tem sessões marcadas deixaria a agenda
            // inconsistente: o usuário precisa remarcá-las antes.
            var sessoesNoPeriodo = await _sessoes.ObterPorPeriodo(veterinarioId, inicio, fim);
            var ativas = sessoesNoPeriodo.Where(s => s.Status != "Cancelada").ToList();

            if (ativas.Count > 0)
            {
                return Resultado<BloqueioAgendaDTO>.Conflito(
                    $"Existem {ativas.Count} sessão(ões) marcada(s) neste período. Remarque-as antes de bloquear a agenda.");
            }

            var bloqueio = new BloqueioAgenda
            {
                VeterinarioId = veterinarioId,
                Inicio = inicio,
                Fim = fim,
                Motivo = dto.Motivo.Trim(),
                CriadoPorId = _usuarioAtual.Id
            };

            await _bloqueios.Adicionar(bloqueio);
            await _bloqueios.SalvarAlteracoes();

            return Resultado<BloqueioAgendaDTO>.Ok(MapearParaDTO(bloqueio, veterinario), "Período bloqueado na agenda.");
        }

        public async Task<Resultado> Remover(Guid id)
        {
            var bloqueio = await _bloqueios.ObterPorId(id);

            if (bloqueio == null || bloqueio.Veterinario?.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Bloqueio não encontrado.");
            }

            if (_usuarioAtual.EhVeterinario && bloqueio.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado.NaoAutorizado("Você só pode remover bloqueios da sua própria agenda.");
            }

            _bloqueios.Remover(bloqueio);
            await _bloqueios.SalvarAlteracoes();

            return Resultado.Ok("Bloqueio removido. O período volta a aceitar agendamentos.");
        }

        private static BloqueioAgendaDTO MapearParaDTO(BloqueioAgenda bloqueio, Veterinario? veterinario)
        {
            return new BloqueioAgendaDTO
            {
                Id = bloqueio.Id,
                VeterinarioId = bloqueio.VeterinarioId,
                NomeVeterinario = (bloqueio.Veterinario ?? veterinario)?.Usuario?.Nome ?? string.Empty,
                Inicio = bloqueio.Inicio,
                Fim = bloqueio.Fim,
                Motivo = bloqueio.Motivo,
                DataCriacao = bloqueio.DataCriacao
            };
        }
    }
}
