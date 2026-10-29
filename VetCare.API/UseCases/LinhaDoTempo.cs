using System.Globalization;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Linha do tempo clínica do animal (HU-011, CA-1): reúne os principais eventos da vida
    /// clínica, tenham ou não originado um tratamento. Cada evento traz data, categoria,
    /// descrição resumida e responsável, e o identificador do registro completo.
    /// </summary>
    public sealed class LinhaDoTempo
    {
        public static class Categorias
        {
            public const string Avaliacao = "Avaliação";
            public const string Tratamento = "Tratamento";
            /// <summary>O registro de cada sessão realizada (a evolução do tratamento).</summary>
            public const string Sessao = "Sessão";
            public const string Cirurgia = "Cirurgia";
            public const string Vacinacao = "Vacinação";
            public const string Receita = "Receita";
            public const string Exame = "Exame";
            public const string Observacao = "Observação";
        }

        private readonly AssinadorDeArquivos _assinador;
        private readonly List<MidiaSessao> _midias;
        private readonly List<ObservacaoInterna> _observacoes;

        /// <param name="observacoes">Vazia para quem não pode vê-las (RN-003).</param>
        public LinhaDoTempo(AssinadorDeArquivos assinador, List<MidiaSessao> midias, List<ObservacaoInterna> observacoes)
        {
            _assinador = assinador;
            _midias = midias;
            _observacoes = observacoes;
        }

        public List<ItemLinhaTempoDTO> Montar(
            List<AvaliacaoClinica> avaliacoes,
            List<Tratamento> tratamentos,
            List<AtendimentoFisioterapeutico> evolucoes,
            List<AlergiaCondicao> registrosClinicos,
            List<VacinaComSituacao> vacinas,
            List<Prescricao> receitas,
            List<PedidoExame> pedidosExame)
        {
            var eventos = new List<ItemLinhaTempoDTO>();

            eventos.AddRange(avaliacoes.Select(a => DeAvaliacao(a, tratamentos)));

            foreach (var tratamento in tratamentos)
            {
                eventos.Add(InicioDoTratamento(tratamento));

                var encerramento = EncerramentoDoTratamento(tratamento);

                if (encerramento != null)
                {
                    eventos.Add(encerramento);
                }
            }

            eventos.AddRange(evolucoes.Select(DeEvolucao));

            // Cirurgias e procedimentos relevantes ficam registrados como alertas clínicos do tipo Cirurgia.
            eventos.AddRange(registrosClinicos
                .Where(r => string.Equals(r.Tipo, "Cirurgia", StringComparison.OrdinalIgnoreCase))
                .Select(DeCirurgia));

            eventos.AddRange(vacinas.Select(v => DeVacina(v.Vacina)));
            eventos.AddRange(receitas.Select(DeReceita));
            eventos.AddRange(pedidosExame.Select(DePedidoExame));

            // Observações internas soltas entram como evento próprio para não se perderem.
            eventos.AddRange(_observacoes
                .Where(o => o.AvaliacaoId == null && o.AtendimentoId == null)
                .Select(DeObservacao));

            return eventos.OrderByDescending(e => e.Data).ToList();
        }

        private ItemLinhaTempoDTO DeAvaliacao(AvaliacaoClinica avaliacao, List<Tratamento> tratamentos)
        {
            var originados = tratamentos.Where(t => t.AvaliacaoId == avaliacao.Id).Select(t => t.Nome).ToList();

            var campos = new Dictionary<string, string>
            {
                ["Motivo da consulta"] = avaliacao.MotivoConsulta,
                ["Observações"] = avaliacao.Observacoes,
                ["Possível diagnóstico"] = avaliacao.DiagnosticoPossivel,
                ["Tratamentos originados"] = originados.Count > 0 ? string.Join(", ", originados) : "Nenhum"
            };

            return new ItemLinhaTempoDTO
            {
                Id = avaliacao.Id,
                Chave = $"avaliacao-{avaliacao.Id}",
                Data = avaliacao.DataRegistro,
                Categoria = Categorias.Avaliacao,
                Tipo = "Avaliação clínica",
                Autor = avaliacao.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = avaliacao.MotivoConsulta,
                Detalhes = $"Possível diagnóstico: {avaliacao.DiagnosticoPossivel}",
                Editado = avaliacao.DataUltimaEdicao.HasValue,
                Campos = campos,
                ObservacoesInternas = ObservacoesDe(o => o.AvaliacaoId == avaliacao.Id)
            };
        }

        private static ItemLinhaTempoDTO InicioDoTratamento(Tratamento tratamento)
        {
            var planejado = tratamento.Status == StatusTratamento.Planejado;

            var campos = new Dictionary<string, string>
            {
                ["Tipo"] = tratamento.Tipo,
                ["Descrição"] = tratamento.Descricao,
                ["Duração prevista"] = tratamento.DuracaoPrevista,
                ["Frequência"] = tratamento.Frequencia,
                ["Sessões previstas"] = tratamento.SessoesPrevistas.ToString(),
                ["Status"] = tratamento.Status
            };

            if (tratamento.Avaliacao != null)
            {
                campos["Avaliação de origem"] =
                    $"{RelogioDaClinica.Padrao.Formatar(tratamento.Avaliacao.DataRegistro, "dd/MM/yyyy")} — {tratamento.Avaliacao.MotivoConsulta}";
            }

            return new ItemLinhaTempoDTO
            {
                Id = tratamento.Id,
                Chave = $"tratamento-inicio-{tratamento.Id}",
                Data = tratamento.DataInicio,
                Categoria = Categorias.Tratamento,
                Tipo = planejado ? "Tratamento planejado" : "Início do tratamento",
                Autor = tratamento.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = $"{tratamento.Nome} ({tratamento.Tipo})",
                Detalhes = tratamento.Descricao,
                TratamentoId = tratamento.Id,
                Campos = campos
            };
        }

        private static ItemLinhaTempoDTO? EncerramentoDoTratamento(Tratamento tratamento)
        {
            if (StatusTratamento.Ativo(tratamento.Status) || tratamento.DataFim == null)
            {
                return null;
            }

            var concluido = tratamento.Status == StatusTratamento.Concluido;

            return new ItemLinhaTempoDTO
            {
                Id = tratamento.Id,
                Chave = $"tratamento-fim-{tratamento.Id}",
                Data = tratamento.DataFim.Value,
                Categoria = Categorias.Tratamento,
                Tipo = concluido ? "Tratamento concluído" : "Tratamento inativado",
                Autor = tratamento.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = tratamento.Nome,
                Detalhes = concluido
                    ? "Tratamento encerrado."
                    : "Tratamento inativado; as sessões registradas continuam no histórico.",
                TratamentoId = tratamento.Id
            };
        }

        private ItemLinhaTempoDTO DeEvolucao(AtendimentoFisioterapeutico evolucao)
        {
            // HU-011, CA-4: as mídias aparecem junto da sessão a que pertencem.
            var midias = _midias
                .Where(m => m.SessaoId == evolucao.SessaoId)
                .Select(m => AnexarMidiaUseCase.MapearParaDTO(m, _assinador))
                .ToList();

            var nomeTratamento = evolucao.Tratamento?.Nome ?? string.Empty;

            return new ItemLinhaTempoDTO
            {
                Id = evolucao.Id,
                Chave = $"evolucao-{evolucao.Id}",
                Data = evolucao.DataRegistro,
                Categoria = Categorias.Sessao,
                Tipo = $"Sessão nº {evolucao.NumeroSessao}",
                Autor = evolucao.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = string.IsNullOrWhiteSpace(nomeTratamento)
                    ? evolucao.TecnicasAplicadas
                    : $"{nomeTratamento} · {evolucao.TecnicasAplicadas}",
                Detalhes = evolucao.EvolucaoClinica,
                Editado = evolucao.DataUltimaEdicao.HasValue,
                TratamentoId = evolucao.TratamentoId,
                NumeroSessao = evolucao.NumeroSessao,
                EscalaDor = evolucao.EscalaDor,
                PesoKg = evolucao.PesoKg,
                Campos = CamposDaEvolucao(evolucao),
                Midias = midias,
                ObservacoesInternas = ObservacoesDe(o => o.AtendimentoId == evolucao.Id)
            };
        }

        private static Dictionary<string, string> CamposDaEvolucao(AtendimentoFisioterapeutico evolucao)
        {
            var campos = new Dictionary<string, string>
            {
                ["Tratamento"] = evolucao.Tratamento?.Nome ?? string.Empty,
                ["Sessão"] = $"nº {evolucao.NumeroSessao}",
                ["Procedimentos realizados"] = evolucao.TecnicasAplicadas,
                ["O que foi realizado"] = evolucao.EvolucaoClinica,
                ["Reação do animal"] = evolucao.ReacaoAnimal,
                ["Escala de dor"] = $"{evolucao.EscalaDor}/10"
            };

            void Opcional(string rotulo, string? valor)
            {
                if (!string.IsNullOrWhiteSpace(valor)) campos[rotulo] = valor;
            }

            Opcional("Observações", evolucao.Observacoes);
            Opcional("Relatório", evolucao.Relatorio);
            Opcional("Próximos passos", evolucao.ProximosPassos);
            Opcional("Sinais vitais", evolucao.SinaisVitais);

            if (evolucao.PesoKg.HasValue)
            {
                campos["Peso"] = $"{ComVirgula(evolucao.PesoKg.Value, "0.##")} kg";
            }

            if (evolucao.TemperaturaCelsius.HasValue)
            {
                campos["Temperatura"] = $"{ComVirgula(evolucao.TemperaturaCelsius.Value, "0.#")} °C";
            }

            if (evolucao.FrequenciaCardiaca.HasValue)
            {
                campos["Frequência cardíaca"] = $"{evolucao.FrequenciaCardiaca.Value} bpm";
            }

            if (evolucao.FrequenciaRespiratoria.HasValue)
            {
                campos["Frequência respiratória"] = $"{evolucao.FrequenciaRespiratoria.Value} mpm";
            }

            return campos;
        }

        /// <summary>
        /// Número com vírgula decimal, como o usuário lê, sem depender da cultura do servidor
        /// (o contêiner da API roda com a cultura invariante).
        /// </summary>
        private static string ComVirgula(decimal valor, string formato) =>
            valor.ToString(formato, CultureInfo.InvariantCulture).Replace('.', ',');

        private static ItemLinhaTempoDTO DeCirurgia(AlergiaCondicao registro)
        {
            return new ItemLinhaTempoDTO
            {
                Id = registro.Id,
                Chave = $"cirurgia-{registro.Id}",
                Data = registro.DataRegistro,
                Categoria = Categorias.Cirurgia,
                Tipo = "Cirurgia / procedimento",
                Autor = registro.RegistradoPor?.Nome ?? string.Empty,
                Descricao = registro.Descricao,
                Detalhes = registro.Ativa ? $"Gravidade: {registro.Gravidade}" : "Registro inativo",
                Campos = new Dictionary<string, string>
                {
                    ["Descrição"] = registro.Descricao,
                    ["Gravidade"] = registro.Gravidade,
                    ["Situação"] = registro.Ativa ? "Ativo" : "Inativo"
                }
            };
        }

        private static ItemLinhaTempoDTO DeVacina(Vacina vacina)
        {
            var dose = vacina.NumeroDose.HasValue ? $" · {GerenciarVacinasUseCase.DescreverDose(vacina)}" : string.Empty;

            var campos = new Dictionary<string, string>
            {
                ["Produto"] = vacina.Nome,
                ["Tipo"] = vacina.Tipo,
                ["Aplicação"] = vacina.DataAplicacao.ToString("dd/MM/yyyy")
            };

            if (vacina.ProximaDose.HasValue)
            {
                campos["Próxima dose"] = vacina.ProximaDose.Value.ToString("dd/MM/yyyy");
            }

            if (!string.IsNullOrWhiteSpace(vacina.Lote))
            {
                campos["Lote"] = vacina.Lote;
            }

            return new ItemLinhaTempoDTO
            {
                Id = vacina.Id,
                Chave = $"vacina-{vacina.Id}",
                // Data de calendário: meia-noite UTC do dia, exibida sem conversão de fuso pelo dia.
                Data = vacina.DataAplicacao,
                Categoria = Categorias.Vacinacao,
                Tipo = vacina.Tipo == "Vacina" ? "Vacinação" : vacina.Tipo,
                Autor = vacina.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = $"{vacina.Nome}{dose}",
                Detalhes = vacina.Observacoes,
                Campos = campos
            };
        }

        private static ItemLinhaTempoDTO DeReceita(Prescricao receita)
        {
            var medicamentos = string.Join(", ", receita.Itens.Select(i => i.Medicamento));
            var origem = GerenciarPrescricoesUseCase.DescreverOrigem(receita);
            var cancelada = receita.Status == StatusPrescricao.Cancelada;

            var campos = new Dictionary<string, string>
            {
                ["Medicamentos"] = medicamentos,
                ["Situação"] = receita.Status
            };

            if (origem.Length > 0)
            {
                campos["Origem"] = origem;
            }

            return new ItemLinhaTempoDTO
            {
                Id = receita.Id,
                Chave = $"receita-{receita.Id}",
                Data = receita.DataEmissao,
                Categoria = Categorias.Receita,
                Tipo = cancelada ? "Receita cancelada" : "Receita emitida",
                Autor = receita.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = medicamentos,
                Detalhes = origem,
                TratamentoId = receita.Atendimento?.TratamentoId,
                Campos = campos
            };
        }

        private static ItemLinhaTempoDTO DePedidoExame(PedidoExame pedido)
        {
            var cancelado = pedido.Status == StatusPedidoExame.Cancelado;
            var exames = pedido.Exames.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var campos = new Dictionary<string, string>
            {
                ["Exames solicitados"] = string.Join(", ", exames.Select(e => e.Trim())),
                ["Situação"] = pedido.Status
            };

            if (!string.IsNullOrWhiteSpace(pedido.Justificativa))
            {
                campos["Justificativa"] = pedido.Justificativa;
            }

            return new ItemLinhaTempoDTO
            {
                Id = pedido.Id,
                Chave = $"exame-{pedido.Id}",
                Data = pedido.DataEmissao,
                Categoria = Categorias.Exame,
                Tipo = cancelado ? "Pedido de exame cancelado" : "Pedido de exame",
                Autor = pedido.Veterinario?.Usuario?.Nome ?? string.Empty,
                Descricao = string.Join(", ", exames.Select(e => e.Trim())),
                Detalhes = pedido.Avaliacao != null
                    ? $"A partir da avaliação de {RelogioDaClinica.Padrao.Formatar(pedido.Avaliacao.DataRegistro, "dd/MM/yyyy")}"
                    : string.Empty,
                Campos = campos
            };
        }

        private static ItemLinhaTempoDTO DeObservacao(ObservacaoInterna observacao)
        {
            return new ItemLinhaTempoDTO
            {
                Id = observacao.Id,
                Chave = $"observacao-{observacao.Id}",
                Data = observacao.DataRegistro,
                Categoria = Categorias.Observacao,
                Tipo = "Observação Interna",
                Autor = observacao.Autor?.Nome ?? string.Empty,
                Descricao = "Anotação restrita à equipe clínica",
                Detalhes = observacao.Conteudo,
                ObservacoesInternas = new List<ObservacaoInternaDTO>
                {
                    RegistrarObservacaoInternaUseCase.MapearParaDTO(observacao)
                }
            };
        }

        private List<ObservacaoInternaDTO> ObservacoesDe(Func<ObservacaoInterna, bool> criterio) =>
            _observacoes.Where(criterio).Select(RegistrarObservacaoInternaUseCase.MapearParaDTO).ToList();
    }
}
