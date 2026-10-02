using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Troca o veterinário responsável por um paciente. O paciente sai da lista do
    /// profissional anterior e entra na do novo, e vão junto o que ainda está por fazer:
    /// os tratamentos em andamento e as sessões futuras. O histórico (avaliações,
    /// atendimentos, sessões concluídas) continua assinado por quem o produziu.
    /// </summary>
    public class TransferirPacienteUseCase
    {
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly ITratamentoRepository _tratamentos;
        private readonly ISessaoRepository _sessoes;
        private readonly IBloqueioAgendaRepository _bloqueios;
        private readonly IClinicaRepository _clinicas;
        private readonly NotificacaoService _notificacoes;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public TransferirPacienteUseCase(
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            ITratamentoRepository tratamentos,
            ISessaoRepository sessoes,
            IBloqueioAgendaRepository bloqueios,
            IClinicaRepository clinicas,
            NotificacaoService notificacoes,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _pets = pets;
            _veterinarios = veterinarios;
            _tratamentos = tratamentos;
            _sessoes = sessoes;
            _bloqueios = bloqueios;
            _clinicas = clinicas;
            _notificacoes = notificacoes;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<ResultadoTransferenciaDTO>> Executar(Guid pacienteId, TransferirPacienteDTO dto)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<ResultadoTransferenciaDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            // Administração e apoio transferem qualquer paciente; o veterinário só pode
            // passar adiante um paciente que está com ele.
            if (_usuarioAtual.EhTutor)
            {
                return Resultado<ResultadoTransferenciaDTO>.NaoAutorizado(
                    "Somente a equipe da clínica pode trocar o veterinário responsável.");
            }

            if (_usuarioAtual.EhVeterinario && !AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<ResultadoTransferenciaDTO>.NaoAutorizado(
                    "Só o veterinário responsável atual ou a administração podem transferir este paciente.");
            }

            if (pet.VeterinarioResponsavelId == dto.VeterinarioId)
            {
                return Resultado<ResultadoTransferenciaDTO>.Invalido("Este veterinário já é o responsável pelo paciente.");
            }

            var novo = await _veterinarios.ObterPorId(dto.VeterinarioId);

            if (novo == null || novo.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<ResultadoTransferenciaDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
            }

            if (novo.Usuario is { Ativo: false })
            {
                return Resultado<ResultadoTransferenciaDTO>.Invalido(
                    "Este veterinário está inativo e não pode receber pacientes.");
            }

            var anterior = pet.VeterinarioResponsavel;
            var nomeAnterior = anterior?.Usuario?.Nome ?? string.Empty;
            var nomeNovo = novo.Usuario?.Nome ?? "o novo veterinário";

            var tratamentosEmAndamento = (await _tratamentos.ObterPorPaciente(pet.Id))
                .Where(t => t.Status == StatusTratamento.EmAndamento)
                .ToList();

            var sessoesFuturas = await ListarSessoesFuturas(tratamentosEmAndamento, novo.Id);

            // RN-002: a agenda de quem recebe precisa comportar o que já estava marcado.
            var conflitos = await ContarConflitos(novo.Id, sessoesFuturas);

            if (conflitos > 0)
            {
                return Resultado<ResultadoTransferenciaDTO>.Conflito(
                    $"A agenda de {nomeNovo} já está ocupada em {conflitos} horário(s) das sessões futuras de {pet.Nome}. " +
                    "Reagende ou cancele essas sessões antes de transferir.");
            }

            foreach (var tratamento in tratamentosEmAndamento)
            {
                var rastreado = await _tratamentos.ObterPorId(tratamento.Id);

                if (rastreado != null)
                {
                    rastreado.VeterinarioId = novo.Id;
                    _tratamentos.Atualizar(rastreado);
                }
            }

            foreach (var sessao in sessoesFuturas)
            {
                var rastreada = await _sessoes.ObterPorId(sessao.Id);

                if (rastreada != null)
                {
                    rastreada.VeterinarioId = novo.Id;
                    _sessoes.Atualizar(rastreada);
                }
            }

            pet.VeterinarioResponsavelId = novo.Id;
            pet.VeterinarioResponsavel = novo;

            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();

            var motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? string.Empty : $" Motivo: {dto.Motivo.Trim()}";

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao,
                "Paciente",
                pet.Id,
                $"Transferência de {pet.Nome}: {(nomeAnterior.Length > 0 ? nomeAnterior : "sem responsável")} → {nomeNovo}." + motivo);

            await Avisar(pet, anterior, novo, nomeNovo);

            var resposta = new ResultadoTransferenciaDTO
            {
                Paciente = GerenciarPacientesUseCase.MapearParaDTO(pet),
                NomeVeterinarioAnterior = nomeAnterior,
                NomeNovoVeterinario = nomeNovo,
                TratamentosTransferidos = tratamentosEmAndamento.Count,
                SessoesTransferidas = sessoesFuturas.Count
            };

            return Resultado<ResultadoTransferenciaDTO>.Ok(resposta, DescreverResultado(pet.Nome, nomeNovo, resposta));
        }

        /// <summary>Sessões ainda por acontecer que estão em nome de outro profissional.</summary>
        private async Task<List<Sessao>> ListarSessoesFuturas(IEnumerable<Tratamento> tratamentos, Guid novoVeterinarioId)
        {
            var agora = DateTime.UtcNow;
            var futuras = new List<Sessao>();

            foreach (var tratamento in tratamentos)
            {
                var sessoes = await _sessoes.ObterPorTratamento(tratamento.Id);

                futuras.AddRange(sessoes.Where(s =>
                    s.DataHora > agora &&
                    s.Status != StatusSessao.Cancelada &&
                    s.Status != StatusSessao.Concluida &&
                    s.VeterinarioId != novoVeterinarioId));
            }

            return futuras;
        }

        private async Task<int> ContarConflitos(Guid veterinarioId, List<Sessao> sessoes)
        {
            if (sessoes.Count == 0)
            {
                return 0;
            }

            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);
            var duracao = clinica?.DuracaoSessaoMinutos ?? 60;
            var conflitos = 0;

            foreach (var sessao in sessoes)
            {
                var ocupado = await _sessoes.ExisteConflitoHorario(veterinarioId, sessao.DataHora, duracao, sessao.Id);
                var bloqueado = await _bloqueios.ObterConflito(veterinarioId, sessao.DataHora, sessao.DataHora.AddMinutes(duracao));

                if (ocupado || bloqueado != null)
                {
                    conflitos++;
                }
            }

            return conflitos;
        }

        /// <summary>Quem recebe, quem entrega e o tutor ficam sabendo; quem fez a troca não precisa de aviso.</summary>
        private async Task Avisar(Pet pet, Veterinario? anterior, Veterinario novo, string nomeNovo)
        {
            if (novo.UsuarioId != _usuarioAtual.Id)
            {
                await _notificacoes.NotificarPacienteRecebido(novo.UsuarioId, pet.Nome, pet.Id);
            }

            if (anterior != null && anterior.UsuarioId != _usuarioAtual.Id)
            {
                await _notificacoes.NotificarPacienteTransferido(anterior.UsuarioId, pet.Nome, nomeNovo);
            }

            var usuarioTutor = pet.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoVeterinarioResponsavel(usuarioTutor.Value, pet.Nome, nomeNovo, pet.Id);
            }
        }

        private static string DescreverResultado(string nomePaciente, string nomeNovo, ResultadoTransferenciaDTO resultado)
        {
            var partes = new List<string>();

            if (resultado.TratamentosTransferidos > 0)
            {
                partes.Add(resultado.TratamentosTransferidos == 1
                    ? "1 tratamento em andamento"
                    : $"{resultado.TratamentosTransferidos} tratamentos em andamento");
            }

            if (resultado.SessoesTransferidas > 0)
            {
                partes.Add(resultado.SessoesTransferidas == 1
                    ? "1 sessão futura"
                    : $"{resultado.SessoesTransferidas} sessões futuras");
            }

            var complemento = partes.Count == 0
                ? string.Empty
                : $" {string.Join(" e ", partes)} passaram para a agenda de {nomeNovo}.";

            return $"{nomePaciente} agora é acompanhado por {nomeNovo}.{complemento}";
        }
    }
}
