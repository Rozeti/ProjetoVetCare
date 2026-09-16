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
    /// não é excluída: ela é cancelada, preservando o histórico (RN-004).
    /// </summary>
    public class GerenciarPrescricoesUseCase
    {
        private readonly IPrescricaoRepository _prescricoes;
        private readonly IPetRepository _pets;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly NotificacaoService _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarPrescricoesUseCase(
            IPrescricaoRepository prescricoes,
            IPetRepository pets,
            IProntuarioRepository prontuarios,
            IVeterinarioRepository veterinarios,
            NotificacaoService notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _prescricoes = prescricoes;
            _pets = pets;
            _prontuarios = prontuarios;
            _veterinarios = veterinarios;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<List<PrescricaoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<PrescricaoDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (_usuarioAtual.EhTutor && pet.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<List<PrescricaoDTO>>.NaoAutorizado("Você não tem acesso a este paciente.");
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

            if (_usuarioAtual.EhTutor && prescricao.Paciente?.TutorId != _usuarioAtual.TutorId)
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

            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PrescricaoDTO>.NaoEncontrado("Paciente não encontrado.");
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

            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(dto.PacienteId);

            var prescricao = new Prescricao
            {
                ProntuarioId = prontuario.Id,
                PacienteId = dto.PacienteId,
                VeterinarioId = veterinarioId,
                AtendimentoId = dto.AtendimentoId,
                ValidaAte = dto.ValidaAte.HasValue
                    ? DateTime.SpecifyKind(dto.ValidaAte.Value.Date, DateTimeKind.Utc)
                    : DateTime.UtcNow.Date.AddDays(30),
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

            if (_usuarioAtual.EhVeterinario && prescricao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado.NaoAutorizado("Você só pode cancelar receitas emitidas por você.");
            }

            if (prescricao.Status == "Cancelada")
            {
                return Resultado.Invalido("Esta receita já está cancelada.");
            }

            prescricao.Status = "Cancelada";

            if (!string.IsNullOrWhiteSpace(motivo))
            {
                var prefixo = string.IsNullOrWhiteSpace(prescricao.Orientacoes)
                    ? string.Empty
                    : prescricao.Orientacoes + " | ";

                prescricao.Orientacoes = $"{prefixo}Cancelada: {motivo.Trim()}";
            }

            _prescricoes.Atualizar(prescricao);
            await _prescricoes.SalvarAlteracoes();

            return Resultado.Ok("Receita cancelada. O registro permanece no histórico do paciente.");
        }

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
