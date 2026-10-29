using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Pedidos de exame gerados a partir de uma avaliação, com ou sem tratamento. O pedido é um
    /// documento assinado por quem solicita, que pode corrigi-lo (a versão anterior fica guardada,
    /// RN-004) ou excluí-lo. Emissão, correção e exclusão ficam na auditoria.
    /// </summary>
    public class GerenciarPedidosExameUseCase
    {
        public const string TipoRegistro = "PedidoExame";

        private readonly IPedidoExameRepository _pedidos;
        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly IPetRepository _pets;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly IVersaoRegistroRepository _versoes;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarPedidosExameUseCase(
            IPedidoExameRepository pedidos,
            IAvaliacaoRepository avaliacoes,
            IPetRepository pets,
            IProntuarioRepository prontuarios,
            IVeterinarioRepository veterinarios,
            IVersaoRegistroRepository versoes,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _pedidos = pedidos;
            _avaliacoes = avaliacoes;
            _pets = pets;
            _prontuarios = prontuarios;
            _veterinarios = veterinarios;
            _versoes = versoes;
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
                AuditoriaService.Acoes.Criacao, TipoRegistro, pedido.Id,
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

        /// <summary>
        /// Correção do pedido: exames, justificativa e, se preciso, a avaliação de origem (do
        /// mesmo animal). A versão anterior fica guardada (RN-004) e a correção vai para a auditoria.
        /// </summary>
        public async Task<Resultado<PedidoExameDTO>> Atualizar(Guid id, AtualizarPedidoExameDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Exames) || dto.Exames.Trim().Length < 2)
            {
                return Resultado<PedidoExameDTO>.Invalido("Informe os exames solicitados.");
            }

            var (pedido, falha, mensagem) = await ObterParaAlterar(id, "editar");

            if (pedido == null)
            {
                return Resultado<PedidoExameDTO>.Erro(falha, mensagem);
            }

            if (pedido.Status == StatusPedidoExame.Cancelado)
            {
                return Resultado<PedidoExameDTO>.Conflito(
                    "Este pedido foi cancelado e não pode ser editado. Se ele não serve mais, exclua-o.");
            }

            Guid? novaOrigem = null;

            // A origem pode passar a ser outra avaliação, desde que do mesmo animal.
            if (dto.AvaliacaoId.HasValue && dto.AvaliacaoId.Value != pedido.AvaliacaoId)
            {
                var avaliacao = await _avaliacoes.ObterPorId(dto.AvaliacaoId.Value);

                if (avaliacao == null || avaliacao.PacienteId != pedido.PacienteId)
                {
                    return Resultado<PedidoExameDTO>.Invalido("A avaliação de origem precisa ser do mesmo animal.");
                }

                novaOrigem = avaliacao.Id;
            }

            await _versoes.Arquivar(TipoRegistro, pedido.Id, new
            {
                pedido.AvaliacaoId,
                pedido.Exames,
                pedido.Justificativa,
                pedido.DataEmissao
            }, _usuarioAtual.Id);

            pedido.Exames = dto.Exames.Trim();
            pedido.Justificativa = (dto.Justificativa ?? string.Empty).Trim();

            if (novaOrigem.HasValue)
            {
                pedido.AvaliacaoId = novaOrigem.Value;
            }

            // O pedido veio rastreado de ObterPorId: basta gravar.
            await _pedidos.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, TipoRegistro, pedido.Id,
                $"Correção do pedido de exame de {pedido.Paciente?.Nome} emitido em " +
                RelogioDaClinica.Padrao.Formatar(pedido.DataEmissao, "dd/MM/yyyy"));

            // Relido para trazer a avaliação de origem atual.
            var atualizado = await _pedidos.ObterPorId(pedido.Id);

            return Resultado<PedidoExameDTO>.Ok(
                MapearParaDTO(atualizado ?? pedido, (atualizado ?? pedido).Paciente),
                "Pedido de exame atualizado.");
        }

        /// <summary>
        /// Exclui o pedido: ele sai do prontuário e da linha do tempo. A auditoria guarda quem
        /// excluiu, quando e o que estava pedido.
        /// </summary>
        public async Task<Resultado> Excluir(Guid id)
        {
            var (pedido, falha, mensagem) = await ObterParaAlterar(id, "excluir");

            if (pedido == null)
            {
                return Resultado.Erro(falha, mensagem);
            }

            var exames = string.Join(", ", pedido.Exames
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var detalhe =
                $"Exclusão do pedido de exame de {pedido.Paciente?.Nome} emitido em " +
                RelogioDaClinica.Padrao.Formatar(pedido.DataEmissao, "dd/MM/yyyy") +
                $": {(exames.Length > 250 ? exames[..250] + "..." : exames)}";

            _pedidos.Remover(pedido);
            await _pedidos.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(AuditoriaService.Acoes.Exclusao, TipoRegistro, id, detalhe);

            return Resultado.Ok("Pedido de exame excluído.");
        }

        /// <summary>
        /// O pedido que quem pede pode alterar: da clínica, de um paciente que ele acessa e, para
        /// o veterinário, assinado por ele mesmo.
        /// </summary>
        private async Task<(PedidoExame? Pedido, TipoFalha Falha, string Mensagem)> ObterParaAlterar(Guid id, string acao)
        {
            var pedido = await _pedidos.ObterPorId(id);

            if (pedido == null || pedido.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return (null, TipoFalha.NaoEncontrado, "Pedido de exame não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pedido.Paciente))
            {
                return (null, TipoFalha.NaoAutorizado, AcessoAoPaciente.MensagemNegada);
            }

            // O veterinário só altera o que ele próprio assinou.
            if (_usuarioAtual.EhVeterinario && pedido.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return (null, TipoFalha.NaoAutorizado, $"Você só pode {acao} pedidos de exame emitidos por você.");
            }

            return (pedido, TipoFalha.Nenhuma, string.Empty);
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
                VeterinarioId = pedido.VeterinarioId,
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
