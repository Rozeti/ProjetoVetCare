using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-011: monta a visão completa do prontuário — alertas clínicos, linha do tempo,
    /// séries de evolução, carteira de vacinação, receitas, documentos e tratamentos.
    ///
    /// RN-003 / RNF-003: as observações internas só são incluídas quando o solicitante é
    /// Administrador ou Veterinário. Para o Tutor elas nem chegam a ser consultadas, de
    /// modo que não existe caminho pelo qual possam vazar na resposta.
    /// </summary>
    public class ConsultarProntuarioUseCase
    {
        private readonly IProntuarioRepository _prontuarios;
        private readonly IPetRepository _pets;
        private readonly ITratamentoRepository _tratamentos;
        private readonly ISessaoRepository _sessoes;
        private readonly IVacinaRepository _vacinas;
        private readonly IPrescricaoRepository _prescricoes;
        private readonly IAlergiaRepository _alergias;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public ConsultarProntuarioUseCase(
            IProntuarioRepository prontuarios,
            IPetRepository pets,
            ITratamentoRepository tratamentos,
            ISessaoRepository sessoes,
            IVacinaRepository vacinas,
            IPrescricaoRepository prescricoes,
            IAlergiaRepository alergias,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _prontuarios = prontuarios;
            _pets = pets;
            _tratamentos = tratamentos;
            _sessoes = sessoes;
            _vacinas = vacinas;
            _prescricoes = prescricoes;
            _alergias = alergias;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<ProntuarioDTO>> Executar(Guid pacienteId)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<ProntuarioDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            // HU-013, CA-1 e CA-2: o tutor só acessa o prontuário dos próprios pets.
            if (_usuarioAtual.EhTutor && pet.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<ProntuarioDTO>.NaoAutorizado("Você não tem acesso ao prontuário deste paciente.");
            }

            var prontuario = await _prontuarios.ObterOuCriarPorPacienteId(pacienteId);

            var avaliacoes = await _prontuarios.ObterAvaliacoes(prontuario.Id);
            var atendimentos = await _prontuarios.ObterAtendimentos(prontuario.Id);
            var midias = await _prontuarios.ObterMidias(prontuario.Id);
            var documentos = await _prontuarios.ObterDocumentos(prontuario.Id);
            var tratamentos = await _tratamentos.ObterPorPaciente(pacienteId);
            var vacinas = await _vacinas.ObterPorPaciente(pacienteId);
            var prescricoes = await _prescricoes.ObterPorProntuario(prontuario.Id);
            var alertas = await _alergias.ObterPorPaciente(pacienteId, apenasAtivas: true);

            var exibeObservacoes = _usuarioAtual.PodeVerObservacoesInternas;

            // A consulta só acontece para quem tem direito de ver (RN-003).
            var observacoes = exibeObservacoes
                ? await _prontuarios.ObterObservacoesInternas(prontuario.Id)
                : new List<ObservacaoInterna>();

            var dto = new ProntuarioDTO
            {
                Id = prontuario.Id,
                PacienteId = pacienteId,
                NomePaciente = pet.Nome,
                Especie = pet.Especie,
                Raca = pet.Raca,
                Sexo = pet.Sexo,
                Pelagem = pet.Pelagem,
                Microchip = pet.Microchip,
                Castrado = pet.Castrado,
                DataNascimento = pet.DataNascimento,
                IdadeAnos = GerenciarPacientesUseCase.CalcularIdade(pet.DataNascimento),
                IdadeDescritiva = GerenciarPacientesUseCase.DescreverIdade(pet.DataNascimento),
                NomeTutor = pet.Tutor?.Usuario?.Nome ?? string.Empty,
                TelefoneTutor = pet.Tutor?.Telefone ?? string.Empty,
                DataCriacao = prontuario.DataCriacao,
                UltimaAtualizacao = prontuario.UltimaAtualizacao,
                DataObito = pet.DataObito,
                ExibeObservacoesInternas = exibeObservacoes,
                AlertasClinicos = alertas.Select(GerenciarAlergiasUseCase.MapearParaDTO).ToList(),
                Historico = MontarLinhaDoTempo(avaliacoes, atendimentos, midias, observacoes),
                EvolucaoPeso = MontarSerieDePeso(atendimentos, pet),
                EvolucaoDor = MontarSerieDeDor(atendimentos),
                Vacinas = vacinas.Select(GerenciarVacinasUseCase.MapearParaDTO).ToList(),
                Prescricoes = prescricoes.Select(p => GerenciarPrescricoesUseCase.MapearParaDTO(p, pet)).ToList(),
                Documentos = documentos.Select(GerenciarDocumentosUseCase.MapearParaDTO).ToList(),
                Tratamentos = await MapearTratamentos(tratamentos)
            };

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Consulta, "Prontuario", prontuario.Id, $"Paciente {pet.Nome}");

            return Resultado<ProntuarioDTO>.Ok(dto);
        }

        /// <summary>HU-011, CA-1: avaliações e atendimentos em ordem cronológica.</summary>
        private static List<ItemLinhaTempoDTO> MontarLinhaDoTempo(
            List<AvaliacaoClinica> avaliacoes,
            List<AtendimentoFisioterapeutico> atendimentos,
            List<MidiaSessao> midias,
            List<ObservacaoInterna> observacoes)
        {
            var historico = new List<ItemLinhaTempoDTO>(avaliacoes.Count + atendimentos.Count);

            foreach (var avaliacao in avaliacoes)
            {
                historico.Add(new ItemLinhaTempoDTO
                {
                    Id = avaliacao.Id,
                    Data = avaliacao.DataRegistro,
                    Tipo = "Avaliação Clínica",
                    Autor = avaliacao.Veterinario?.Usuario?.Nome ?? string.Empty,
                    Descricao = avaliacao.QueixaPrincipal,
                    Detalhes = avaliacao.PlanoTerapeutico,
                    Editado = avaliacao.DataUltimaEdicao.HasValue,
                    Campos = new Dictionary<string, string>
                    {
                        ["Queixa principal"] = avaliacao.QueixaPrincipal,
                        ["Anamnese"] = avaliacao.Anamnese,
                        ["Exame físico"] = avaliacao.ExameFisico,
                        ["Hipótese diagnóstica"] = avaliacao.HipoteseDiagnostica,
                        ["Plano terapêutico"] = avaliacao.PlanoTerapeutico
                    },
                    ObservacoesInternas = observacoes
                        .Where(o => o.AvaliacaoId == avaliacao.Id)
                        .Select(RegistrarObservacaoInternaUseCase.MapearParaDTO)
                        .ToList()
                });
            }

            foreach (var atendimento in atendimentos)
            {
                // HU-011, CA-4: as mídias aparecem junto da sessão a que pertencem.
                var midiasDoAtendimento = midias
                    .Where(m => m.SessaoId == atendimento.SessaoId)
                    .Select(AnexarMidiaUseCase.MapearParaDTO)
                    .ToList();

                historico.Add(new ItemLinhaTempoDTO
                {
                    Id = atendimento.Id,
                    Data = atendimento.DataRegistro,
                    Tipo = "Atendimento",
                    Autor = atendimento.Veterinario?.Usuario?.Nome ?? string.Empty,
                    Descricao = $"Dor {atendimento.EscalaDor}/10 — {atendimento.TecnicasAplicadas}",
                    Detalhes = atendimento.EvolucaoClinica,
                    Editado = atendimento.DataUltimaEdicao.HasValue,
                    EscalaDor = atendimento.EscalaDor,
                    PesoKg = atendimento.PesoKg,
                    Campos = MontarCamposDoAtendimento(atendimento),
                    Midias = midiasDoAtendimento,
                    ObservacoesInternas = observacoes
                        .Where(o => o.AtendimentoId == atendimento.Id)
                        .Select(RegistrarObservacaoInternaUseCase.MapearParaDTO)
                        .ToList()
                });
            }

            // Observações internas soltas entram como item próprio para não se perderem.
            foreach (var observacao in observacoes.Where(o => o.AvaliacaoId == null && o.AtendimentoId == null))
            {
                historico.Add(new ItemLinhaTempoDTO
                {
                    Id = observacao.Id,
                    Data = observacao.DataRegistro,
                    Tipo = "Observação Interna",
                    Autor = observacao.Autor?.Nome ?? string.Empty,
                    Descricao = "Anotação restrita à equipe clínica",
                    Detalhes = observacao.Conteudo,
                    ObservacoesInternas = new List<ObservacaoInternaDTO>
                    {
                        RegistrarObservacaoInternaUseCase.MapearParaDTO(observacao)
                    }
                });
            }

            return historico.OrderByDescending(h => h.Data).ToList();
        }

        private static Dictionary<string, string> MontarCamposDoAtendimento(AtendimentoFisioterapeutico atendimento)
        {
            var campos = new Dictionary<string, string>
            {
                ["Técnicas aplicadas"] = atendimento.TecnicasAplicadas,
                ["Escala de dor"] = $"{atendimento.EscalaDor}/10",
                ["Evolução clínica"] = atendimento.EvolucaoClinica
            };

            if (!string.IsNullOrWhiteSpace(atendimento.ProximosPassos))
            {
                campos["Próximos passos"] = atendimento.ProximosPassos;
            }

            if (!string.IsNullOrWhiteSpace(atendimento.SinaisVitais))
            {
                campos["Sinais vitais"] = atendimento.SinaisVitais;
            }

            if (atendimento.PesoKg.HasValue)
            {
                campos["Peso"] = $"{atendimento.PesoKg.Value:0.##} kg";
            }

            if (atendimento.TemperaturaCelsius.HasValue)
            {
                campos["Temperatura"] = $"{atendimento.TemperaturaCelsius.Value:0.#} °C";
            }

            if (atendimento.FrequenciaCardiaca.HasValue)
            {
                campos["Frequência cardíaca"] = $"{atendimento.FrequenciaCardiaca.Value} bpm";
            }

            if (atendimento.FrequenciaRespiratoria.HasValue)
            {
                campos["Frequência respiratória"] = $"{atendimento.FrequenciaRespiratoria.Value} mpm";
            }

            return campos;
        }

        /// <summary>HU-011, CA-3: série de peso ao longo do tempo para o gráfico de evolução.</summary>
        private static List<PontoEvolucaoDTO> MontarSerieDePeso(List<AtendimentoFisioterapeutico> atendimentos, Pet pet)
        {
            var serie = atendimentos
                .Where(a => a.PesoKg.HasValue)
                .OrderBy(a => a.DataRegistro)
                .Select(a => new PontoEvolucaoDTO { Data = a.DataRegistro, Valor = a.PesoKg!.Value })
                .ToList();

            // Sem registros de peso nos atendimentos, o peso do cadastro serve de ponto único.
            if (serie.Count == 0 && pet.PesoAtualKg.HasValue)
            {
                serie.Add(new PontoEvolucaoDTO { Data = DateTime.UtcNow, Valor = pet.PesoAtualKg.Value });
            }

            return serie;
        }

        private static List<PontoEvolucaoDTO> MontarSerieDeDor(List<AtendimentoFisioterapeutico> atendimentos)
        {
            return atendimentos
                .OrderBy(a => a.DataRegistro)
                .Select(a => new PontoEvolucaoDTO { Data = a.DataRegistro, Valor = a.EscalaDor })
                .ToList();
        }

        private async Task<List<TratamentoDTO>> MapearTratamentos(List<Tratamento> tratamentos)
        {
            var lista = new List<TratamentoDTO>(tratamentos.Count);

            foreach (var tratamento in tratamentos)
            {
                var sessoes = await _sessoes.ObterPorTratamento(tratamento.Id);

                lista.Add(new TratamentoDTO
                {
                    Id = tratamento.Id,
                    PacienteId = tratamento.PacienteId,
                    VeterinarioId = tratamento.VeterinarioId,
                    NomeVeterinario = tratamento.Veterinario?.Usuario?.Nome ?? string.Empty,
                    DataInicio = tratamento.DataInicio,
                    DataFim = tratamento.DataFim,
                    ObjetivoTerapeutico = tratamento.ObjetivoTerapeutico,
                    Status = tratamento.Status,
                    ObservacoesGerais = tratamento.ObservacoesGerais,
                    TotalSessoes = sessoes.Count,
                    SessoesConcluidas = sessoes.Count(s => s.Status == "Concluída")
                });
            }

            return lista;
        }
    }
}
