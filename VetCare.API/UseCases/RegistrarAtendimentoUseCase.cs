using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-008: registro do atendimento fisioterapêutico de cada sessão.</summary>
    public class RegistrarAtendimentoUseCase
    {
        public const string TipoRegistro = "AtendimentoFisioterapeutico";

        private readonly IAtendimentoRepository _atendimentos;
        private readonly ISessaoRepository _sessoes;
        private readonly IProntuarioRepository _prontuarios;
        private readonly IPetRepository _pets;
        private readonly IMidiaRepository _midias;
        private readonly IObservacaoInternaRepository _observacoes;
        private readonly IVersaoRegistroRepository _versoes;
        private readonly NotificacaoService _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public RegistrarAtendimentoUseCase(
            IAtendimentoRepository atendimentos,
            ISessaoRepository sessoes,
            IProntuarioRepository prontuarios,
            IPetRepository pets,
            IMidiaRepository midias,
            IObservacaoInternaRepository observacoes,
            IVersaoRegistroRepository versoes,
            NotificacaoService notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _atendimentos = atendimentos;
            _sessoes = sessoes;
            _prontuarios = prontuarios;
            _pets = pets;
            _midias = midias;
            _observacoes = observacoes;
            _versoes = versoes;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<AtendimentoDTO>> Executar(CriarAtendimentoDTO dto)
        {
            // HU-008, CA-2: a escala de dor precisa estar entre 0 e 10.
            if (dto.EscalaDor < 0 || dto.EscalaDor > 10)
            {
                return Resultado<AtendimentoDTO>.Invalido("A escala de dor deve ser um valor entre 0 e 10.");
            }

            if (string.IsNullOrWhiteSpace(dto.TecnicasAplicadas) || string.IsNullOrWhiteSpace(dto.EvolucaoClinica))
            {
                return Resultado<AtendimentoDTO>.Invalido(
                    "Informe as técnicas aplicadas e a evolução clínica do atendimento.");
            }

            var sessao = await _sessoes.ObterPorIdComRelacionamentos(dto.SessaoId);

            if (sessao == null)
            {
                return Resultado<AtendimentoDTO>.NaoEncontrado("Sessão não encontrada.");
            }

            if (sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<AtendimentoDTO>.NaoAutorizado("Esta sessão pertence a outra clínica.");
            }

            if (sessao.Status == "Cancelada")
            {
                return Resultado<AtendimentoDTO>.Invalido(
                    "Não é possível registrar atendimento em uma sessão cancelada.");
            }

            // Uma sessão gera no máximo um atendimento (cardinalidade 1..0..1 do diagrama de classes).
            if (await _atendimentos.ObterPorSessao(dto.SessaoId) != null)
            {
                return Resultado<AtendimentoDTO>.Conflito("Esta sessão já possui um atendimento registrado.");
            }

            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId ?? sessao.VeterinarioId;

            if (_usuarioAtual.EhVeterinario && _usuarioAtual.VeterinarioId != veterinarioId)
            {
                return Resultado<AtendimentoDTO>.NaoAutorizado("O atendimento deve ser registrado em seu próprio nome.");
            }

            var pacienteId = sessao.Tratamento!.PacienteId;
            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pacienteId);

            var atendimento = new AtendimentoFisioterapeutico
            {
                SessaoId = dto.SessaoId,
                TratamentoId = sessao.TratamentoId,
                ProntuarioId = prontuario.Id,
                VeterinarioId = veterinarioId,
                TecnicasAplicadas = dto.TecnicasAplicadas.Trim(),
                EscalaDor = dto.EscalaDor,
                EvolucaoClinica = dto.EvolucaoClinica.Trim(),
                SinaisVitais = dto.SinaisVitais.Trim(),
                ProximosPassos = dto.ProximosPassos.Trim(),
                PesoKg = dto.PesoKg,
                TemperaturaCelsius = dto.TemperaturaCelsius,
                FrequenciaCardiaca = dto.FrequenciaCardiaca,
                FrequenciaRespiratoria = dto.FrequenciaRespiratoria
            };

            await _atendimentos.Adicionar(atendimento);
            await _atendimentos.SalvarAlteracoes();

            if (!string.IsNullOrWhiteSpace(dto.ObservacaoInterna))
            {
                await _observacoes.Adicionar(new ObservacaoInterna
                {
                    ProntuarioId = prontuario.Id,
                    AtendimentoId = atendimento.Id,
                    AutorId = _usuarioAtual.Id,
                    Conteudo = dto.ObservacaoInterna.Trim()
                });

                await _observacoes.SalvarAlteracoes();
            }

            // O peso mais recente do atendimento passa a ser o peso atual do paciente.
            if (dto.PesoKg.HasValue)
            {
                await AtualizarPesoDoPaciente(pacienteId, dto.PesoKg.Value);
            }

            if (dto.ConcluirSessao && sessao.Status != "Concluída")
            {
                sessao.Status = "Concluída";
                _sessoes.Atualizar(sessao);
                await _sessoes.SalvarAlteracoes();
            }

            prontuario.UltimaAtualizacao = DateTime.UtcNow;
            _prontuarios.Atualizar(prontuario);
            await _prontuarios.SalvarAlteracoes();

            var usuarioTutor = sessao.Tratamento?.Paciente?.Tutor?.UsuarioId;

            if (usuarioTutor.HasValue)
            {
                await _notificacoes.NotificarNovoRegistroProntuario(
                    usuarioTutor.Value,
                    sessao.Tratamento?.Paciente?.Nome ?? "seu pet",
                    "Novo atendimento",
                    pacienteId);
            }

            return Resultado<AtendimentoDTO>.Ok(
                await MapearParaDTO(atendimento),
                "Atendimento registrado com sucesso.");
        }

        /// <summary>RN-004: a edição arquiva a versão anterior antes de aplicar as mudanças.</summary>
        public async Task<Resultado<AtendimentoDTO>> Atualizar(Guid id, AtualizarAtendimentoDTO dto)
        {
            if (dto.EscalaDor < 0 || dto.EscalaDor > 10)
            {
                return Resultado<AtendimentoDTO>.Invalido("A escala de dor deve ser um valor entre 0 e 10.");
            }

            var atendimento = await _atendimentos.ObterPorId(id);

            if (atendimento == null)
            {
                return Resultado<AtendimentoDTO>.NaoEncontrado("Atendimento não encontrado.");
            }

            if (_usuarioAtual.EhVeterinario && atendimento.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado<AtendimentoDTO>.NaoAutorizado(
                    "Você só pode corrigir atendimentos registrados por você.");
            }

            await _versoes.Arquivar(TipoRegistro, atendimento.Id, new
            {
                atendimento.TecnicasAplicadas,
                atendimento.EscalaDor,
                atendimento.EvolucaoClinica,
                atendimento.SinaisVitais,
                atendimento.ProximosPassos,
                atendimento.PesoKg,
                atendimento.TemperaturaCelsius,
                atendimento.FrequenciaCardiaca,
                atendimento.FrequenciaRespiratoria,
                atendimento.DataRegistro,
                atendimento.DataUltimaEdicao
            }, _usuarioAtual.Id);

            atendimento.TecnicasAplicadas = dto.TecnicasAplicadas.Trim();
            atendimento.EscalaDor = dto.EscalaDor;
            atendimento.EvolucaoClinica = dto.EvolucaoClinica.Trim();
            atendimento.SinaisVitais = dto.SinaisVitais.Trim();
            atendimento.ProximosPassos = dto.ProximosPassos.Trim();
            atendimento.PesoKg = dto.PesoKg;
            atendimento.TemperaturaCelsius = dto.TemperaturaCelsius;
            atendimento.FrequenciaCardiaca = dto.FrequenciaCardiaca;
            atendimento.FrequenciaRespiratoria = dto.FrequenciaRespiratoria;
            atendimento.DataUltimaEdicao = DateTime.UtcNow;

            _atendimentos.Atualizar(atendimento);
            await _atendimentos.SalvarAlteracoes();

            return Resultado<AtendimentoDTO>.Ok(
                await MapearParaDTO(atendimento),
                "Atendimento atualizado. A versão anterior foi preservada no histórico.");
        }

        public async Task<Resultado<AtendimentoDTO>> ObterPorId(Guid id)
        {
            var atendimento = await _atendimentos.ObterPorId(id);

            return atendimento == null
                ? Resultado<AtendimentoDTO>.NaoEncontrado("Atendimento não encontrado.")
                : Resultado<AtendimentoDTO>.Ok(await MapearParaDTO(atendimento));
        }

        public async Task<Resultado<AtendimentoDTO?>> ObterPorSessao(Guid sessaoId)
        {
            var atendimento = await _atendimentos.ObterPorSessao(sessaoId);

            if (atendimento == null)
            {
                return Resultado<AtendimentoDTO?>.Ok(null);
            }

            var completo = await _atendimentos.ObterPorId(atendimento.Id);
            return Resultado<AtendimentoDTO?>.Ok(await MapearParaDTO(completo!));
        }

        public async Task<Resultado<List<HistoricoVersaoDTO>>> ObterHistorico(Guid id)
        {
            var versoes = await _versoes.ObterHistorico(TipoRegistro, id);

            var dtos = versoes.Select(v => new HistoricoVersaoDTO
            {
                Id = v.Id,
                TipoRegistro = v.TipoRegistro,
                RegistroId = v.RegistroId,
                ConteudoAnterior = v.ConteudoAnterior,
                AlteradoPor = v.AlteradoPor?.Nome ?? string.Empty,
                DataAlteracao = v.DataAlteracao
            }).ToList();

            return Resultado<List<HistoricoVersaoDTO>>.Ok(dtos);
        }

        private async Task AtualizarPesoDoPaciente(Guid pacienteId, decimal peso)
        {
            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null)
            {
                return;
            }

            pet.PesoAtualKg = peso;
            _pets.Atualizar(pet);
            await _pets.SalvarAlteracoes();
        }

        private async Task<AtendimentoDTO> MapearParaDTO(AtendimentoFisioterapeutico atendimento)
        {
            var midias = await _midias.ObterPorSessao(atendimento.SessaoId);

            return new AtendimentoDTO
            {
                Id = atendimento.Id,
                SessaoId = atendimento.SessaoId,
                TratamentoId = atendimento.TratamentoId,
                ProntuarioId = atendimento.ProntuarioId,
                DataRegistro = atendimento.DataRegistro,
                DataUltimaEdicao = atendimento.DataUltimaEdicao,
                NomeVeterinario = atendimento.Veterinario?.Usuario?.Nome ?? string.Empty,
                TecnicasAplicadas = atendimento.TecnicasAplicadas,
                EscalaDor = atendimento.EscalaDor,
                EvolucaoClinica = atendimento.EvolucaoClinica,
                SinaisVitais = atendimento.SinaisVitais,
                ProximosPassos = atendimento.ProximosPassos,
                PesoKg = atendimento.PesoKg,
                TemperaturaCelsius = atendimento.TemperaturaCelsius,
                FrequenciaCardiaca = atendimento.FrequenciaCardiaca,
                FrequenciaRespiratoria = atendimento.FrequenciaRespiratoria,
                Midias = midias.Select(AnexarMidiaUseCase.MapearParaDTO).ToList()
            };
        }
    }
}
