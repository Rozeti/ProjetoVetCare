using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Pedidos de exame gerados a partir de uma avaliação, com ou sem tratamento. Como a receita,
    /// o pedido é um documento assinado por quem solicita: é cancelado, nunca apagado (RN-004),
    /// e emissão e cancelamento ficam na auditoria.
    /// </summary>
    public class GerenciarPedidosExameUseCase
    {
        private const int TamanhoDoMotivo = 300;

        private readonly IPedidoExameRepository _pedidos;
        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly IPetRepository _pets;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarPedidosExameUseCase(
            IPedidoExameRepository pedidos,
            IAvaliacaoRepository avaliacoes,
            IPetRepository pets,
            IProntuarioRepository prontuarios,
            IVeterinarioRepository veterinarios,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _pedidos = pedidos;
            _avaliacoes = avaliacoes;
            _pets = pets;
            _prontuarios = prontuarios;
            _veterinarios = veterinarios;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<PedidoExameDTO>> Emitir(CriarPedidoExameDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Exames) || dto.Exames.Trim().Length < 2)
            {
                return Resultado<PedidoExameDTO>.Invalido("Informe os exames solicitados.");
            }

            var avaliacao = await _avaliacoes.ObterPorId(dto.AvaliacaoId);

            if (avaliacao == null || avaliacao.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PedidoExameDTO>.NaoEncontrado("Avaliação de origem não encontrada.");
            }

            var pet = await _pets.ObterPorIdComTutor(avaliacao.PacienteId);

            if (pet == null)
            {
                return Resultado<PedidoExameDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<PedidoExameDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (!pet.EmAcompanhamento)
            {
                return Resultado<PedidoExameDTO>.Conflito(
                    "O paciente está inativo ou tem óbito registrado; o prontuário fica apenas para consulta.");
            }

            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? avaliacao.VeterinarioId;

            // O pedido é um documento assinado por quem solicita.
            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<PedidoExameDTO>.NaoAutorizado("O pedido de exame deve ser emitido em seu próprio nome.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PedidoExameDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pet.Id);

            var pedido = new PedidoExame
            {
                PacienteId = pet.Id,
                ProntuarioId = prontuario.Id,
                AvaliacaoId = avaliacao.Id,
                VeterinarioId = veterinario.Id,
                Exames = dto.Exames.Trim(),
                Justificativa = (dto.Justificativa ?? string.Empty).Trim()
            };

            await _pedidos.Adicionar(pedido);
            await _pedidos.SalvarAlteracoes();

            pedido.Paciente = pet;
            pedido.Veterinario = veterinario;
            pedido.Avaliacao = avaliacao;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "PedidoExame", pedido.Id,
                $"Pedido de exame de {pet.Nome}, assinado por {veterinario.Usuario?.Nome}, a partir da avaliação de " +
                RelogioDaClinica.Padrao.Formatar(avaliacao.DataRegistro, "dd/MM/yyyy"));

            var usuarioTutor = pet.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value, pet.Nome, "Novo pedido de exame", pet.Id);
            }

            return Resultado<PedidoExameDTO>.Ok(MapearParaDTO(pedido, pet), "Pedido de exame emitido com sucesso.");
        }

        public async Task<Resultado<List<PedidoExameDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<PedidoExameDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<List<PedidoExameDTO>>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pacienteId);
            var pedidos = await _pedidos.ObterPorProntuario(prontuario.Id);

            return Resultado<List<PedidoExameDTO>>.Ok(pedidos.Select(p => MapearParaDTO(p, pet)).ToList());
        }

        public async Task<Resultado<PedidoExameDTO>> ObterPorId(Guid id)
        {
            var pedido = await _pedidos.ObterPorId(id);

            if (pedido == null || pedido.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PedidoExameDTO>.NaoEncontrado("Pedido de exame não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pedido.Paciente))
            {
                return Resultado<PedidoExameDTO>.NaoAutorizado("Você não tem acesso a este pedido de exame.");
            }

            return Resultado<PedidoExameDTO>.Ok(MapearParaDTO(pedido, pedido.Paciente));
        }

        public async Task<Resultado> Cancelar(Guid id, string? motivo)
        {
            var pedido = await _pedidos.ObterPorId(id);

            if (pedido == null || pedido.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Pedido de exame não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pedido.Paciente))
            {
                return Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            // O veterinário só cancela o que ele próprio assinou.
            if (_usuarioAtual.EhVeterinario && pedido.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado.NaoAutorizado("Você só pode cancelar pedidos de exame emitidos por você.");
            }

            if (pedido.Status == StatusPedidoExame.Cancelado)
            {
                return Resultado.Conflito("Este pedido de exame já está cancelado.");
            }

            var motivoLimpo = (motivo ?? string.Empty).Trim();

            pedido.Status = StatusPedidoExame.Cancelado;
            pedido.MotivoCancelamento = motivoLimpo.Length <= TamanhoDoMotivo ? motivoLimpo : motivoLimpo[..TamanhoDoMotivo];

            _pedidos.Atualizar(pedido);
            await _pedidos.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Inativacao, "PedidoExame", pedido.Id,
                $"Cancelamento do pedido de exame de {pedido.Paciente?.Nome} emitido em " +
                RelogioDaClinica.Padrao.Formatar(pedido.DataEmissao, "dd/MM/yyyy") +
                (motivoLimpo.Length > 0 ? $". Motivo: {pedido.MotivoCancelamento}" : string.Empty));

            return Resultado.Ok("Pedido de exame cancelado. O registro permanece no histórico do paciente.");
        }

        public static PedidoExameDTO MapearParaDTO(PedidoExame pedido, Pet? pet)
        {
            return new PedidoExameDTO
            {
                Id = pedido.Id,
                PacienteId = pedido.PacienteId,
                NomePaciente = pet?.Nome ?? pedido.Paciente?.Nome ?? string.Empty,
                Especie = pet?.Especie ?? pedido.Paciente?.Especie ?? string.Empty,
                Raca = pet?.Raca ?? pedido.Paciente?.Raca ?? string.Empty,
                NomeTutor = pet?.Tutor?.Usuario?.Nome ?? pedido.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                AvaliacaoId = pedido.AvaliacaoId,
                DataAvaliacao = pedido.Avaliacao?.DataRegistro,
                MotivoAvaliacao = pedido.Avaliacao?.MotivoConsulta ?? string.Empty,
                NomeVeterinario = pedido.Veterinario?.Usuario?.Nome ?? string.Empty,
                Crmv = pedido.Veterinario?.Crmv ?? string.Empty,
                DataEmissao = pedido.DataEmissao,
                Exames = pedido.Exames,
                Justificativa = pedido.Justificativa,
                Status = pedido.Status,
                MotivoCancelamento = pedido.MotivoCancelamento
            };
        }
    }
}
