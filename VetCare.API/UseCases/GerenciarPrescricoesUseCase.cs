using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Receituário do paciente. Assim como os demais registros clínicos, uma receita
    /// não é excluída: ela é cancelada, preservando o histórico (RN-004). Emissão e
    /// cancelamento ficam na auditoria: é o documento assinado com CRMV que sai da clínica.
    /// </summary>
    public class GerenciarPrescricoesUseCase
    {
        /// <summary>Validade assumida quando o veterinário não informa uma data.</summary>
        public const int ValidadePadraoEmDias = 30;

        /// <summary>Teto para a validade informada: uma receita não vale indefinidamente.</summary>
        public const int ValidadeMaximaEmDias = 365;

        /// <summary>Tamanho da coluna de orientações, respeitado também ao anotar o cancelamento.</summary>
        private const int TamanhoDasOrientacoes = 2000;

        private readonly IPrescricaoRepository _prescricoes;
        private readonly IPetRepository _pets;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarPrescricoesUseCase(
            IPrescricaoRepository prescricoes,
            IPetRepository pets,
            IProntuarioRepository prontuarios,
            IVeterinarioRepository veterinarios,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _prescricoes = prescricoes;
            _pets = pets;
            _prontuarios = prontuarios;
            _veterinarios = veterinarios;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<PrescricaoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<PrescricaoDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<List<PrescricaoDTO>>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pacienteId);
            var prescricoes = await _prescricoes.ObterPorProntuario(prontuario.Id);

            return Resultado<List<PrescricaoDTO>>.Ok(prescricoes.Select(p => MapearParaDTO(p, pet)).ToList());
        }

        public async Task<Resultado<PrescricaoDTO>> ObterPorId(Guid id)
        {
            var prescricao = await _prescricoes.ObterPorId(id);

            if (prescricao == null || prescricao.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PrescricaoDTO>.NaoEncontrado("Receita não encontrada.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, prescricao.Paciente))
            {
                return Resultado<PrescricaoDTO>.NaoAutorizado("Você não tem acesso a esta receita.");
            }

            return Resultado<PrescricaoDTO>.Ok(MapearParaDTO(prescricao, prescricao.Paciente));
        }

        public async Task<Resultado<PrescricaoDTO>> Emitir(CriarPrescricaoDTO dto)
        {
            if (dto.Itens.Count == 0)
            {
                return Resultado<PrescricaoDTO>.Invalido("Informe ao menos um medicamento na receita.");
            }

            if (dto.Itens.Any(i => string.IsNullOrWhiteSpace(i.Medicamento)))
            {
                return Resultado<PrescricaoDTO>.Invalido("Todo item da receita precisa do nome do medicamento.");
            }

            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PrescricaoDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<PrescricaoDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (!pet.EmAcompanhamento)
            {
                return Resultado<PrescricaoDTO>.Conflito(
                    "O paciente está inativo ou tem óbito registrado; o prontuário fica apenas para consulta.");
            }

            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? Guid.Empty;

            if (veterinarioId == Guid.Empty)
            {
                return Resultado<PrescricaoDTO>.Invalido("Informe o veterinário responsável pela receita.");
            }

            var veterinario = await _veterinarios.ObterPorId(veterinarioId);

            if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PrescricaoDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            // A receita é um documento assinado por quem prescreve.
            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<PrescricaoDTO>.NaoAutorizado("A receita deve ser emitida em seu próprio nome.");
            }

            // A validade conta no calendário da clínica: hoje já emitida vencida não faz sentido.
            var hoje = RelogioDaClinica.Padrao.Hoje;
            var validaAte = dto.ValidaAte.HasValue
                ? DateTime.SpecifyKind(dto.ValidaAte.Value.Date, DateTimeKind.Utc)
                : DateTime.SpecifyKind(hoje.AddDays(ValidadePadraoEmDias), DateTimeKind.Utc);

            if (validaAte.Date <= hoje)
            {
                return Resultado<PrescricaoDTO>.Invalido("A validade da receita deve ser uma data futura.");
            }

            if (validaAte.Date > hoje.AddDays(ValidadeMaximaEmDias))
            {
                return Resultado<PrescricaoDTO>.Invalido(
                    $"A validade da receita não pode passar de {ValidadeMaximaEmDias} dias.");
            }

            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(dto.PacienteId);

            var prescricao = new Prescricao
            {
                ProntuarioId = prontuario.Id,
                PacienteId = dto.PacienteId,
                VeterinarioId = veterinarioId,
                AtendimentoId = dto.AtendimentoId,
                ValidaAte = validaAte,
                Orientacoes = dto.Orientacoes.Trim()
            };

            foreach (var item in dto.Itens)
            {
                prescricao.Itens.Add(new ItemPrescricao
                {
                    PrescricaoId = prescricao.Id,
                    Medicamento = item.Medicamento.Trim(),
                    Dosagem = item.Dosagem.Trim(),
                    Frequencia = item.Frequencia.Trim(),
                    Duracao = item.Duracao.Trim(),
                    Via = item.Via.Trim(),
                    Observacao = item.Observacao.Trim()
                });
            }

            await _prescricoes.Adicionar(prescricao);
            await _prescricoes.SalvarAlteracoes();

            prescricao.Paciente = pet;
            prescricao.Veterinario = veterinario;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Prescricao", prescricao.Id,
                $"Receita de {pet.Nome} com {prescricao.Itens.Count} item(ns), assinada por {veterinario.Usuario?.Nome}");

            var usuarioTutor = pet.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value, pet.Nome, "Nova receita emitida", pet.Id);
            }

            return Resultado<PrescricaoDTO>.Ok(MapearParaDTO(prescricao, pet), "Receita emitida com sucesso.");
        }

        public async Task<Resultado> Cancelar(Guid id, string? motivo)
        {
            var prescricao = await _prescricoes.ObterPorId(id);

            if (prescricao == null || prescricao.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Receita não encontrada.");
            }

            // A mesma regra de acesso do restante do prontuário, mais a autoria: o veterinário
            // só cancela o que ele próprio assinou.
            if (!AcessoAoPaciente.Permitido(_usuarioAtual, prescricao.Paciente))
            {
                return Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (_usuarioAtual.EhVeterinario && prescricao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado.NaoAutorizado("Você só pode cancelar receitas emitidas por você.");
            }

            if (prescricao.Status == StatusPrescricao.Cancelada)
            {
                return Resultado.Conflito("Esta receita já está cancelada.");
            }

            prescricao.Status = StatusPrescricao.Cancelada;

            var motivoLimpo = (motivo ?? string.Empty).Trim();

            if (motivoLimpo.Length > 0)
            {
                var prefixo = string.IsNullOrWhiteSpace(prescricao.Orientacoes)
                    ? string.Empty
                    : prescricao.Orientacoes + " | ";

                // O motivo vai para as orientações, mas nunca além do que a coluna comporta.
                prescricao.Orientacoes = Limitar($"{prefixo}Cancelada: {motivoLimpo}", TamanhoDasOrientacoes);
            }

            _prescricoes.Atualizar(prescricao);
            await _prescricoes.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Inativacao, "Prescricao", prescricao.Id,
                $"Cancelamento da receita de {prescricao.Paciente?.Nome} emitida em {prescricao.DataEmissao:dd/MM/yyyy}" +
                (motivoLimpo.Length > 0 ? $". Motivo: {motivoLimpo}" : string.Empty));

            return Resultado.Ok("Receita cancelada. O registro permanece no histórico do paciente.");
        }

        private static string Limitar(string valor, int tamanho) =>
            valor.Length <= tamanho ? valor : valor[..tamanho];

        public static PrescricaoDTO MapearParaDTO(Prescricao prescricao, Pet? pet)
        {
            return new PrescricaoDTO
            {
                Id = prescricao.Id,
                PacienteId = prescricao.PacienteId,
                NomePaciente = pet?.Nome ?? prescricao.Paciente?.Nome ?? string.Empty,
                Especie = pet?.Especie ?? prescricao.Paciente?.Especie ?? string.Empty,
                Raca = pet?.Raca ?? prescricao.Paciente?.Raca ?? string.Empty,
                NomeTutor = pet?.Tutor?.Usuario?.Nome ?? prescricao.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                NomeVeterinario = prescricao.Veterinario?.Usuario?.Nome ?? string.Empty,
                Crmv = prescricao.Veterinario?.Crmv ?? string.Empty,
                DataEmissao = prescricao.DataEmissao,
                ValidaAte = prescricao.ValidaAte,
                Orientacoes = prescricao.Orientacoes,
                Status = prescricao.Status,
                Itens = prescricao.Itens.Select(i => new ItemPrescricaoDTO
                {
                    Id = i.Id,
                    Medicamento = i.Medicamento,
                    Dosagem = i.Dosagem,
                    Frequencia = i.Frequencia,
                    Duracao = i.Duracao,
                    Via = i.Via,
                    Observacao = i.Observacao
                }).ToList()
            };
        }
    }
}
