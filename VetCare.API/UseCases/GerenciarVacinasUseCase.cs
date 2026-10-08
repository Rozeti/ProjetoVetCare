using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Carteira de vacinação e vermifugação do paciente. O controle da próxima dose
    /// é o que sustenta a rotina de prevenção acompanhada pelo tutor. Esquemas com
    /// várias doses são conferidos dose a dose, a recorrência calcula o reforço quando
    /// a data não é informada, e correções e exclusões ficam na auditoria: a carteira
    /// é um documento do animal.
    /// </summary>
    public class GerenciarVacinasUseCase
    {
        public static readonly string[] TiposValidos = { "Vacina", "Vermifugo", "Antipulgas", "Outro" };
        public static readonly string[] RecorrenciasValidas = { "Nenhuma", "Mensal", "Trimestral", "Semestral", "Anual" };
        public static readonly string[] SituacoesValidas = { "Em dia", "A vencer", "Vencida", "Dose única", "Concluída" };

        public const string SemRecorrencia = "Nenhuma";
        public const int MaximoDeDoses = 10;

        private readonly IVacinaRepository _vacinas;
        private readonly IPetRepository _pets;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarVacinasUseCase(
            IVacinaRepository vacinas,
            IPetRepository pets,
            IVeterinarioRepository veterinarios,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _vacinas = vacinas;
            _pets = pets;
            _veterinarios = veterinarios;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        /// <summary>Carteira do paciente com filtro por tipo, situação e texto, paginada (RNF-004).</summary>
        public async Task<Resultado<PaginaDe<VacinaDTO>>> ListarPorPaciente(
            Guid pacienteId,
            string? tipo,
            string? situacao,
            string? busca,
            ParametrosPagina parametros)
        {
            var pet = await _pets.ObterPorIdComTutor(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<PaginaDe<VacinaDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<PaginaDe<VacinaDTO>>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (!string.IsNullOrWhiteSpace(tipo) && !TiposValidos.Contains(tipo, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<PaginaDe<VacinaDTO>>.Invalido(
                    $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}.");
            }

            if (!string.IsNullOrWhiteSpace(situacao) && !SituacoesValidas.Contains(situacao, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<PaginaDe<VacinaDTO>>.Invalido(
                    $"Situação inválida. Use uma destas: {string.Join(", ", SituacoesValidas)}.");
            }

            var filtro = new FiltroDeVacinas
            {
                Tipo = string.IsNullOrWhiteSpace(tipo) ? null : Normalizar(tipo, TiposValidos),
                Situacao = string.IsNullOrWhiteSpace(situacao) ? null : Normalizar(situacao, SituacoesValidas),
                Busca = busca
            };

            var pagina = await _vacinas.ListarPorPaciente(pacienteId, filtro, parametros);

            return Resultado<PaginaDe<VacinaDTO>>.Ok(pagina.Converter(MapearParaDTO));
        }

        /// <summary>Painel de prevenção da clínica: o que vence nos próximos dias.</summary>
        public async Task<Resultado<List<VacinaDTO>>> ListarVencendo(int dias)
        {
            var janela = Math.Clamp(dias, 1, 365);
            var vacinas = await _vacinas.ObterVencendo(_usuarioAtual.ClinicaId, janela);

            // RN-008: o veterinário só vê a prevenção dos pacientes sob sua responsabilidade.
            if (_usuarioAtual.EhVeterinario)
            {
                vacinas = vacinas
                    .Where(v => AcessoAoPaciente.Permitido(_usuarioAtual, v.Vacina.Paciente))
                    .ToList();
            }

            return Resultado<List<VacinaDTO>>.Ok(vacinas.Select(MapearParaDTO).ToList());
        }

        public async Task<Resultado<VacinaDTO>> Registrar(CriarVacinaDTO dto)
        {
            var pet = await _pets.ObterPorIdComTutor(dto.PacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<VacinaDTO>.NaoEncontrado("Paciente não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<VacinaDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (pet.DataObito.HasValue)
            {
                return Resultado<VacinaDTO>.Conflito("O paciente tem óbito registrado; a carteira ficou apenas para consulta.");
            }

            var esquema = ValidarEsquema(
                dto.Tipo, dto.Nome, dto.NumeroDose, dto.TotalDoses, dto.Recorrencia, dto.DataAplicacao, dto.ProximaDose);

            if (!esquema.Sucesso)
            {
                return Resultado<VacinaDTO>.Erro(esquema.Falha, esquema.Mensagem);
            }

            var dados = esquema.Dados!;

            var sequencia = await ValidarSequenciaDoEsquema(
                dto.PacienteId, dados.Nome, dto.NumeroDose, dto.TotalDoses, dados.Aplicacao, ignorarId: null);

            if (sequencia != null)
            {
                return Resultado<VacinaDTO>.Invalido(sequencia);
            }

            // O aplicador, quando informado, precisa ser um veterinário desta clínica.
            Veterinario? veterinario = null;
            var veterinarioId = dto.VeterinarioId ?? _usuarioAtual.VeterinarioId;

            if (veterinarioId.HasValue)
            {
                veterinario = await _veterinarios.ObterPorId(veterinarioId.Value);

                if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<VacinaDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
                }
            }

            var vacina = new Vacina
            {
                PacienteId = dto.PacienteId,
                VeterinarioId = veterinario?.Id,
                Tipo = dados.Tipo,
                Nome = dados.Nome,
                Fabricante = dto.Fabricante.Trim(),
                Lote = dto.Lote.Trim(),
                NumeroDose = dto.NumeroDose,
                TotalDoses = dto.TotalDoses,
                Recorrencia = dados.Recorrencia,
                DataAplicacao = dados.Aplicacao,
                ProximaDose = dados.ProximaDose,
                Observacoes = dto.Observacoes.Trim()
            };

            await _vacinas.Adicionar(vacina);
            await _vacinas.SalvarAlteracoes();

            vacina.Paciente = pet;
            vacina.Veterinario = veterinario;

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Vacina", vacina.Id,
                $"{DescreverDose(vacina)} de {vacina.Nome} aplicada em {pet.Nome}");

            return Resultado<VacinaDTO>.Ok(
                MapearParaDTO(new VacinaComSituacao(vacina, false)),
                "Registro adicionado à carteira do paciente.");
        }

        public async Task<Resultado<VacinaDTO>> Atualizar(Guid id, AtualizarVacinaDTO dto)
        {
            var vacina = await _vacinas.ObterPorId(id);

            if (vacina == null || vacina.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<VacinaDTO>.NaoEncontrado("Registro não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, vacina.Paciente))
            {
                return Resultado<VacinaDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var esquema = ValidarEsquema(
                dto.Tipo, dto.Nome, dto.NumeroDose, dto.TotalDoses, dto.Recorrencia, dto.DataAplicacao, dto.ProximaDose);

            if (!esquema.Sucesso)
            {
                return Resultado<VacinaDTO>.Erro(esquema.Falha, esquema.Mensagem);
            }

            var dados = esquema.Dados!;

            var sequencia = await ValidarSequenciaDoEsquema(
                vacina.PacienteId, dados.Nome, dto.NumeroDose, dto.TotalDoses, dados.Aplicacao, ignorarId: vacina.Id);

            if (sequencia != null)
            {
                return Resultado<VacinaDTO>.Invalido(sequencia);
            }

            if (dto.VeterinarioId.HasValue && dto.VeterinarioId != vacina.VeterinarioId)
            {
                var veterinario = await _veterinarios.ObterPorId(dto.VeterinarioId.Value);

                if (veterinario == null || veterinario.Usuario?.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return Resultado<VacinaDTO>.NaoEncontrado("Veterinário não encontrado nesta clínica.");
                }

                vacina.VeterinarioId = veterinario.Id;
                vacina.Veterinario = veterinario;
            }

            vacina.Tipo = dados.Tipo;
            vacina.Nome = dados.Nome;
            vacina.Fabricante = dto.Fabricante.Trim();
            vacina.Lote = dto.Lote.Trim();
            vacina.NumeroDose = dto.NumeroDose;
            vacina.TotalDoses = dto.TotalDoses;
            vacina.Recorrencia = dados.Recorrencia;
            vacina.DataAplicacao = dados.Aplicacao;
            vacina.Observacoes = dto.Observacoes.Trim();

            // Uma nova data reabre o lembrete, que pode já ter sido enviado antes.
            if (vacina.ProximaDose != dados.ProximaDose)
            {
                vacina.ProximaDose = dados.ProximaDose;
                vacina.LembreteEnviado = false;
            }

            _vacinas.Atualizar(vacina);
            await _vacinas.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Vacina", vacina.Id,
                $"Correção de {vacina.Nome} ({DescreverDose(vacina)}) na carteira de {vacina.Paciente?.Nome}");

            // A situação depende das outras aplicações do produto; a lista completa é a fonte.
            var atualizada = (await _vacinas.ObterPorPaciente(vacina.PacienteId))
                .FirstOrDefault(v => v.Vacina.Id == vacina.Id);

            return Resultado<VacinaDTO>.Ok(
                MapearParaDTO(atualizada ?? new VacinaComSituacao(vacina, false)),
                "Registro atualizado.");
        }

        /// <summary>
        /// Apaga um registro lançado por engano. A carteira não tem versões como o
        /// prontuário, então a auditoria guarda o que foi removido e por quê.
        /// </summary>
        public async Task<Resultado> Remover(Guid id, string justificativa)
        {
            var motivo = (justificativa ?? string.Empty).Trim();

            if (motivo.Length < 5)
            {
                return Resultado.Invalido("Informe a justificativa da exclusão (mínimo de 5 caracteres).");
            }

            var vacina = await _vacinas.ObterPorId(id);

            if (vacina == null || vacina.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Registro não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, vacina.Paciente))
            {
                return Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            var descricao =
                $"{vacina.Tipo} {vacina.Nome} ({DescreverDose(vacina)}) de {vacina.DataAplicacao:dd/MM/yyyy} " +
                $"removida da carteira de {vacina.Paciente?.Nome}. Justificativa: {motivo}";

            _vacinas.Remover(vacina);
            await _vacinas.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(AuditoriaService.Acoes.Exclusao, "Vacina", id, descricao);

            return Resultado.Ok("Registro removido da carteira.");
        }

        private sealed record EsquemaValidado(string Tipo, string Nome, string Recorrencia, DateTime Aplicacao, DateTime? ProximaDose);

        /// <summary>
        /// Regras de uma aplicação, iguais no registro e na correção: tipo e recorrência
        /// conhecidos, número da dose dentro do esquema, aplicação não futura e próxima
        /// dose depois da aplicação. Sem data informada, a recorrência calcula o reforço;
        /// um esquema ainda em aberto não pode ficar sem a data da dose seguinte.
        /// </summary>
        private static Resultado<EsquemaValidado> ValidarEsquema(
            string tipo,
            string nome,
            int? numeroDose,
            int? totalDoses,
            string? recorrencia,
            DateTime dataAplicacao,
            DateTime? proximaDose)
        {
            if (!TiposValidos.Contains(tipo, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<EsquemaValidado>.Invalido(
                    $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}.");
            }

            var nomeLimpo = (nome ?? string.Empty).Trim();

            if (nomeLimpo.Length < 2)
            {
                return Resultado<EsquemaValidado>.Invalido("Informe o nome do produto aplicado.");
            }

            var recorrenciaInformada = string.IsNullOrWhiteSpace(recorrencia) ? SemRecorrencia : recorrencia.Trim();

            if (!RecorrenciasValidas.Contains(recorrenciaInformada, StringComparer.OrdinalIgnoreCase))
            {
                return Resultado<EsquemaValidado>.Invalido(
                    $"Recorrência inválida. Use uma destas: {string.Join(", ", RecorrenciasValidas)}.");
            }

            var recorrenciaNormalizada = Normalizar(recorrenciaInformada, RecorrenciasValidas);

            if (numeroDose.HasValue != totalDoses.HasValue)
            {
                return Resultado<EsquemaValidado>.Invalido(
                    "Para registrar um esquema vacinal, informe o número desta dose e o total de doses.");
            }

            if (totalDoses.HasValue && (totalDoses.Value < 1 || totalDoses.Value > MaximoDeDoses))
            {
                return Resultado<EsquemaValidado>.Invalido($"O esquema deve ter entre 1 e {MaximoDeDoses} doses.");
            }

            if (numeroDose.HasValue && (numeroDose.Value < 1 || numeroDose.Value > totalDoses!.Value))
            {
                return Resultado<EsquemaValidado>.Invalido(
                    $"O número da dose deve estar entre 1 e {totalDoses} (total do esquema).");
            }

            var aplicacao = DateTime.SpecifyKind(dataAplicacao.Date, DateTimeKind.Utc);

            if (aplicacao > RelogioDaClinica.Padrao.Hoje)
            {
                return Resultado<EsquemaValidado>.Invalido("A data de aplicação não pode ser futura.");
            }

            var proxima = proximaDose.HasValue
                ? DateTime.SpecifyKind(proximaDose.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;

            var esquemaEmAberto = numeroDose.HasValue && numeroDose.Value < totalDoses!.Value;

            if (!proxima.HasValue)
            {
                if (recorrenciaNormalizada != SemRecorrencia)
                {
                    proxima = CalcularReforco(aplicacao, recorrenciaNormalizada);
                }
                else if (esquemaEmAberto)
                {
                    return Resultado<EsquemaValidado>.Invalido(
                        $"A dose {numeroDose} de {totalDoses} de {nomeLimpo} exige a data da próxima dose do esquema " +
                        "(ou uma recorrência para calculá-la).");
                }
            }

            if (proxima.HasValue && proxima.Value <= aplicacao)
            {
                return Resultado<EsquemaValidado>.Invalido("A próxima dose deve ser posterior à data de aplicação.");
            }

            return Resultado<EsquemaValidado>.Ok(new EsquemaValidado(
                Normalizar(tipo, TiposValidos), nomeLimpo, recorrenciaNormalizada, aplicacao, proxima));
        }

        /// <summary>
        /// Esquema com mais de uma dose: a dose N só entra depois da dose N-1 do mesmo
        /// produto, com o mesmo total. A dose 1 sempre pode ser registrada — é assim que
        /// um esquema interrompido recomeça.
        /// </summary>
        private async Task<string?> ValidarSequenciaDoEsquema(
            Guid pacienteId,
            string nome,
            int? numeroDose,
            int? totalDoses,
            DateTime aplicacao,
            Guid? ignorarId)
        {
            if (!numeroDose.HasValue || numeroDose.Value == 1)
            {
                return null;
            }

            var anterior = await _vacinas.ObterUltimaAplicacaoDoProduto(pacienteId, nome, aplicacao, ignorarId);

            if (anterior == null)
            {
                return $"Não há dose anterior de {nome} na carteira: a dose {numeroDose} exige a dose " +
                       $"{numeroDose - 1} aplicada antes de {aplicacao:dd/MM/yyyy}.";
            }

            if (!anterior.NumeroDose.HasValue)
            {
                return $"A aplicação anterior de {nome} ({anterior.DataAplicacao:dd/MM/yyyy}) foi registrada sem " +
                       "número de dose. Corrija aquele registro para montar o esquema.";
            }

            if (anterior.NumeroDose.Value == anterior.TotalDoses)
            {
                return $"O esquema de {nome} foi concluído em {anterior.DataAplicacao:dd/MM/yyyy} " +
                       $"(dose {anterior.NumeroDose} de {anterior.TotalDoses}); um novo esquema começa pela dose 1.";
            }

            if (anterior.NumeroDose.Value != numeroDose.Value - 1)
            {
                return $"A última dose registrada de {nome} é a {anterior.NumeroDose} de {anterior.TotalDoses}; " +
                       $"a próxima deve ser a dose {anterior.NumeroDose.Value + 1}.";
            }

            if (anterior.TotalDoses != totalDoses)
            {
                return $"O esquema de {nome} foi iniciado com {anterior.TotalDoses} doses; mantenha o mesmo total.";
            }

            return null;
        }

        private static DateTime? CalcularReforco(DateTime aplicacao, string recorrencia) => recorrencia switch
        {
            "Mensal" => aplicacao.AddMonths(1),
            "Trimestral" => aplicacao.AddMonths(3),
            "Semestral" => aplicacao.AddMonths(6),
            "Anual" => aplicacao.AddYears(1),
            _ => null
        };

        private static string Normalizar(string valor, string[] validos) =>
            validos.FirstOrDefault(v => string.Equals(v, valor, StringComparison.OrdinalIgnoreCase)) ?? valor;

        public static string DescreverDose(Vacina vacina) =>
            vacina.NumeroDose.HasValue ? $"Dose {vacina.NumeroDose} de {vacina.TotalDoses}" : "Dose avulsa";

        public static VacinaDTO MapearParaDTO(VacinaComSituacao item)
        {
            var vacina = item.Vacina;

            var dias = vacina.ProximaDose.HasValue && !item.DoseSeguinteAplicada
                ? (int)(vacina.ProximaDose.Value.Date - RelogioDaClinica.Padrao.Hoje).TotalDays
                : (int?)null;

            return new VacinaDTO
            {
                Id = vacina.Id,
                PacienteId = vacina.PacienteId,
                NomePaciente = vacina.Paciente?.Nome ?? string.Empty,
                NomeTutor = vacina.Paciente?.Tutor?.Usuario?.Nome ?? string.Empty,
                Tipo = vacina.Tipo,
                Nome = vacina.Nome,
                Fabricante = vacina.Fabricante,
                Lote = vacina.Lote,
                NumeroDose = vacina.NumeroDose,
                TotalDoses = vacina.TotalDoses,
                DescricaoDose = vacina.NumeroDose.HasValue ? DescreverDose(vacina) : string.Empty,
                Recorrencia = string.IsNullOrWhiteSpace(vacina.Recorrencia) ? SemRecorrencia : vacina.Recorrencia,
                DataAplicacao = vacina.DataAplicacao,
                ProximaDose = vacina.ProximaDose,
                Observacoes = vacina.Observacoes,
                VeterinarioId = vacina.VeterinarioId,
                AplicadaPor = vacina.Veterinario?.Usuario?.Nome ?? string.Empty,
                SituacaoDose = ClassificarSituacao(vacina.ProximaDose, dias, item.DoseSeguinteAplicada),
                DiasParaProximaDose = dias
            };
        }

        /// <summary>
        /// Classificação usada pela interface para destacar o que exige ação: a janela de
        /// 30 dias dá tempo de agendar a aplicação antes do vencimento. Uma dose cujo
        /// reforço já foi aplicado está concluída, não vencida.
        /// </summary>
        private static string ClassificarSituacao(DateTime? proximaDose, int? diasParaProximaDose, bool doseSeguinteAplicada)
        {
            if (!proximaDose.HasValue)
            {
                return "Dose única";
            }

            if (doseSeguinteAplicada)
            {
                return "Concluída";
            }

            return diasParaProximaDose switch
            {
                < 0 => "Vencida",
                <= VacinaRepository.DiasDeAntecedenciaDoAviso => "A vencer",
                _ => "Em dia"
            };
        }
    }
}
