using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Pedidos de exame feitos em uma avaliação ou em um tratamento (direto nele ou no registro de
    /// uma das sessões dele); o pedido de uma avaliação pode ainda ficar ligado a um tratamento.
    /// O pedido é um documento assinado por quem solicita, que pode corrigi-lo (a versão anterior
    /// fica guardada, RN-004) ou excluí-lo. Emissão, correção e exclusão ficam na auditoria.
    /// </summary>
    public class GerenciarPedidosExameUseCase
    {
        public const string TipoRegistro = "PedidoExame";

        private readonly IPedidoExameRepository _pedidos;
        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly IAtendimentoRepository _atendimentos;
        private readonly ITratamentoRepository _tratamentos;
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
            IAtendimentoRepository atendimentos,
            ITratamentoRepository tratamentos,
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
            _atendimentos = atendimentos;
            _tratamentos = tratamentos;
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

            // Uma origem só: a avaliação, a sessão ou, sem as duas, o próprio tratamento.
            if (dto.AvaliacaoId.HasValue && dto.AtendimentoId.HasValue ||
                !dto.AvaliacaoId.HasValue && !dto.AtendimentoId.HasValue && !dto.TratamentoId.HasValue)
            {
                return Resultado<PedidoExameDTO>.Invalido(
                    "Informe a origem do pedido: a avaliação ou o tratamento em que os exames foram pedidos.");
            }

            AvaliacaoClinica? avaliacao = null;
            AtendimentoFisioterapeutico? atendimento = null;
            Guid? pacienteId;
            string naoEncontrada;

            if (dto.AtendimentoId.HasValue)
            {
                atendimento = await _atendimentos.ObterPorId(dto.AtendimentoId.Value);
                pacienteId = atendimento?.Tratamento?.PacienteId;
                naoEncontrada = "Sessão de origem não encontrada.";
            }
            else if (dto.AvaliacaoId.HasValue)
            {
                avaliacao = await _avaliacoes.ObterPorId(dto.AvaliacaoId.Value);
                pacienteId = avaliacao?.PacienteId;
                naoEncontrada = "Avaliação de origem não encontrada.";
            }
            else
            {
                // Pedido feito no tratamento: o paciente é o do tratamento, validado adiante.
                pacienteId = (await _tratamentos.ObterPorId(dto.TratamentoId!.Value))?.PacienteId;
                naoEncontrada = "Tratamento não encontrado.";
            }

            var pet = pacienteId.HasValue ? await _pets.ObterPorIdComTutor(pacienteId.Value) : null;

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PedidoExameDTO>.NaoEncontrado(naoEncontrada);
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

            // O tratamento do pedido: o da sessão, o próprio tratamento de origem ou o vínculo
            // opcional escolhido na avaliação.
            var (tratamento, erroDoTratamento) = await VinculoComTratamento.Resolver(
                _tratamentos, pet.Id, atendimento, dto.TratamentoId, ehOrigem: avaliacao == null && atendimento == null);

            if (erroDoTratamento != null)
            {
                return Resultado<PedidoExameDTO>.Invalido(erroDoTratamento);
            }

            // Sem veterinário logado, assina quem atendeu a sessão, quem fez a avaliação ou o
            // responsável pelo tratamento.
            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ??
                                atendimento?.VeterinarioId ?? avaliacao?.VeterinarioId ?? tratamento!.VeterinarioId;

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
                // A avaliação só é a origem no pedido feito nela; no do tratamento, fica vazia.
                AvaliacaoId = avaliacao?.Id,
                AtendimentoId = atendimento?.Id,
                TratamentoId = tratamento?.Id,
                VeterinarioId = veterinario.Id,
                Exames = dto.Exames.Trim(),
                Justificativa = (dto.Justificativa ?? string.Empty).Trim()
            };

            await _pedidos.Adicionar(pedido);
            await _pedidos.SalvarAlteracoes();

            pedido.Paciente = pet;
            pedido.Veterinario = veterinario;
            pedido.Avaliacao = avaliacao;
            pedido.Atendimento = atendimento;
            pedido.Tratamento = tratamento;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, TipoRegistro, pedido.Id,
                $"Pedido de exame de {pet.Nome}, assinado por {veterinario.Usuario?.Nome}, feito n" +
                OrigemClinica.ParaAuditoria(avaliacao, atendimento, tratamento));

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
        /// Correção do pedido: exames, justificativa e, no pedido de uma avaliação, a avaliação de
        /// origem (do mesmo animal) e o tratamento vinculado. O pedido feito no tratamento ou em uma
        /// sessão continua ligado ao tratamento. A versão anterior fica guardada (RN-004) e a
        /// correção vai para a auditoria.
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
            var feitoNoTratamento = TipoDeOrigem(pedido) == OrigemClinica.Tratamento;

            // A origem pode passar a ser outra avaliação, desde que do mesmo animal; o pedido feito
            // no tratamento (ou em uma sessão dele) não troca de origem.
            if (feitoNoTratamento && dto.AvaliacaoId.HasValue && dto.AvaliacaoId.Value != pedido.AvaliacaoId)
            {
                return Resultado<PedidoExameDTO>.Invalido(
                    (pedido.AtendimentoId.HasValue
                        ? "Este pedido foi feito em uma sessão e continua ligado ao tratamento dela."
                        : "Este pedido foi feito no tratamento e continua ligado a ele.") +
                    " Para outra origem, emita um novo pedido.");
            }

            if (!feitoNoTratamento && dto.AvaliacaoId.HasValue && dto.AvaliacaoId.Value != pedido.AvaliacaoId)
            {
                var avaliacao = await _avaliacoes.ObterPorId(dto.AvaliacaoId.Value);

                if (avaliacao == null || avaliacao.PacienteId != pedido.PacienteId)
                {
                    return Resultado<PedidoExameDTO>.Invalido("A avaliação de origem precisa ser do mesmo animal.");
                }

                novaOrigem = avaliacao.Id;
            }

            // O tratamento do pedido feito no tratamento não muda; o do pedido de uma avaliação é
            // o enviado na correção (vazio, o pedido fica sem tratamento).
            var tratamento = pedido.Tratamento;

            if (!feitoNoTratamento)
            {
                var (escolhido, erroDoTratamento) = await VinculoComTratamento.Resolver(
                    _tratamentos, pedido.PacienteId, null, dto.TratamentoId, pedido.TratamentoId);

                if (erroDoTratamento != null)
                {
                    return Resultado<PedidoExameDTO>.Invalido(erroDoTratamento);
                }

                tratamento = escolhido;
            }

            await _versoes.Arquivar(TipoRegistro, pedido.Id, new
            {
                pedido.AvaliacaoId,
                pedido.TratamentoId,
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

            pedido.TratamentoId = tratamento?.Id;
            pedido.Tratamento = tratamento;

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

        /// <summary>"Avaliação" ou "Tratamento" (também o pedido feito em uma sessão).</summary>
        public static string TipoDeOrigem(PedidoExame pedido) =>
            OrigemClinica.Tipo(pedido.AvaliacaoId, pedido.AtendimentoId, pedido.TratamentoId);

        /// <summary>"Avaliação de 12/04/2026" ou "Tratamento Hidroterapia".</summary>
        public static string DescreverOrigem(PedidoExame pedido) =>
            OrigemClinica.Descrever(TipoDeOrigem(pedido), pedido.Avaliacao, pedido.Tratamento ?? pedido.Atendimento?.Tratamento);

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
                AtendimentoId = pedido.AtendimentoId,
                NumeroSessao = pedido.Atendimento?.NumeroSessao,
                TipoOrigem = TipoDeOrigem(pedido),
                Origem = DescreverOrigem(pedido),
                TratamentoId = pedido.TratamentoId,
                NomeTratamento = pedido.Tratamento?.Nome ?? string.Empty,
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
