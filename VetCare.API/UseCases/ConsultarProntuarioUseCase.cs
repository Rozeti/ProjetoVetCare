using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-011: monta a visão completa do prontuário — alertas clínicos, avaliações com os
    /// tratamentos que originaram, linha do tempo clínica, séries de evolução, carteira de
    /// vacinação, receitas, pedidos de exame, documentos e tratamentos.
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
        private readonly IPedidoExameRepository _pedidosExame;
        private readonly IAlergiaRepository _alergias;
        private readonly AuditoriaService _auditoria;
        private readonly AssinadorDeArquivos _assinador;
        private readonly UsuarioAtual _usuarioAtual;

        public ConsultarProntuarioUseCase(
            IProntuarioRepository prontuarios,
            IPetRepository pets,
            ITratamentoRepository tratamentos,
            ISessaoRepository sessoes,
            IVacinaRepository vacinas,
            IPrescricaoRepository prescricoes,
            IPedidoExameRepository pedidosExame,
            IAlergiaRepository alergias,
            AuditoriaService auditoria,
            AssinadorDeArquivos assinador,
            UsuarioAtual usuarioAtual)
        {
            _prontuarios = prontuarios;
            _pets = pets;
            _tratamentos = tratamentos;
            _sessoes = sessoes;
            _vacinas = vacinas;
            _prescricoes = prescricoes;
            _pedidosExame = pedidosExame;
            _alergias = alergias;
            _auditoria = auditoria;
            _assinador = assinador;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<ProntuarioDTO>> Executar(Guid pacienteId)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<ProntuarioDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            // HU-013, CA-1 e CA-2: o tutor só acessa o prontuário dos próprios pets; a equipe
            // clínica, inclusive qualquer veterinário, acessa o de todos os pacientes da clínica.
            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
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
            var pedidosExame = await _pedidosExame.ObterPorProntuario(prontuario.Id);
            var registrosClinicos = await _alergias.ObterPorPaciente(pacienteId, apenasAtivas: false);

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
                VeterinarioResponsavelId = pet.VeterinarioResponsavelId,
                NomeVeterinarioResponsavel = pet.VeterinarioResponsavel?.Usuario?.Nome ?? string.Empty,
                DataCriacao = prontuario.DataCriacao,
                UltimaAtualizacao = prontuario.UltimaAtualizacao,
                DataObito = pet.DataObito,
                ExibeObservacoesInternas = exibeObservacoes,
                AlertasClinicos = registrosClinicos.Where(a => a.Ativa).Select(GerenciarAlergiasUseCase.MapearParaDTO).ToList(),
                Avaliacoes = avaliacoes
                    .Select(a => RegistrarAvaliacaoUseCase.MapearParaDTO(
                        a,
                        tratamentos,
                        prescricoes.Count(p => p.AvaliacaoId == a.Id),
                        pedidosExame.Count(p => p.AvaliacaoId == a.Id)))
                    .ToList(),
                Historico = new LinhaDoTempo(_assinador, midias, observacoes).Montar(
                    avaliacoes, tratamentos, atendimentos, registrosClinicos, vacinas, prescricoes, pedidosExame),
                EvolucaoPeso = MontarSerieDePeso(atendimentos, pet),
                EvolucaoDor = MontarSerieDeDor(atendimentos),
                Vacinas = vacinas.Select(GerenciarVacinasUseCase.MapearParaDTO).ToList(),
                Prescricoes = prescricoes.Select(p => GerenciarPrescricoesUseCase.MapearParaDTO(p, pet)).ToList(),
                PedidosExame = pedidosExame.Select(p => GerenciarPedidosExameUseCase.MapearParaDTO(p, pet)).ToList(),
                Documentos = documentos.Select(d => GerenciarDocumentosUseCase.MapearParaDTO(d, _assinador)).ToList(),
                Tratamentos = await MapearTratamentos(tratamentos, atendimentos)
            };

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Consulta, "Prontuario", prontuario.Id, $"Paciente {pet.Nome}");

            return Resultado<ProntuarioDTO>.Ok(dto);
        }

        /// <summary>HU-011, CA-3: série de peso ao longo do tempo para o gráfico de evolução.</summary>
        private static List<PontoEvolucaoDTO> MontarSerieDePeso(List<AtendimentoFisioterapeutico> atendimentos, Pet pet)
        {
            var serie = atendimentos
                .Where(a => a.PesoKg.HasValue)
                .OrderBy(a => a.DataRegistro)
                .Select(a => new PontoEvolucaoDTO { Data = a.DataRegistro, Valor = a.PesoKg!.Value })
                .ToList();

            // Sem registros de peso nas evoluções, o peso do cadastro serve de ponto único.
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

        /// <summary>Os totais de sessões saem numa única consulta; as evoluções já estão em mãos.</summary>
        private async Task<List<TratamentoDTO>> MapearTratamentos(
            List<Tratamento> tratamentos,
            List<AtendimentoFisioterapeutico> atendimentos)
        {
            var contagens = await _sessoes.ContarPorTratamentos(tratamentos.Select(t => t.Id));

            return tratamentos
                .Select(t => GerenciarTratamentosUseCase.MapearParaDTO(
                    t,
                    contagens.GetValueOrDefault(t.Id, new ContagemDeSessoes(0, 0)),
                    atendimentos.Count(a => a.TratamentoId == t.Id)))
                .ToList();
        }
    }
}
