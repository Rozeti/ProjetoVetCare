using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Models;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Vínculo de receitas e pedidos de exame com o tratamento. Quando o registro nasce no
    /// tratamento (direto nele ou em uma das sessões dele), o tratamento é a própria origem;
    /// quando nasce de uma avaliação, o vínculo é opcional. Em todo caso o tratamento precisa ser
    /// do mesmo animal e, para um vínculo novo, estar planejado ou em andamento.
    /// </summary>
    public static class VinculoComTratamento
    {
        public const string MensagemDaSessao =
            "Receitas e pedidos de exame feitos em uma sessão ficam ligados ao tratamento da própria sessão.";

        /// <summary>
        /// Devolve o tratamento do registro (ou nenhum) e, quando a escolha não vale, a mensagem
        /// que a recusa. <paramref name="atual"/> é o tratamento já ligado ao registro em uma
        /// correção: ele continua aceito mesmo que tenha sido concluído depois.
        /// <paramref name="ehOrigem"/> indica que o tratamento é a origem do registro (emitido no
        /// próprio tratamento), e não um vínculo opcional de uma avaliação.
        /// </summary>
        public static async Task<(Tratamento? Tratamento, string? Erro)> Resolver(
            ITratamentoRepository tratamentos,
            Guid pacienteId,
            AtendimentoFisioterapeutico? sessao,
            Guid? escolhido,
            Guid? atual = null,
            bool ehOrigem = false)
        {
            if (sessao != null)
            {
                if (escolhido.HasValue && escolhido.Value != sessao.TratamentoId)
                {
                    return (null, MensagemDaSessao);
                }

                return (sessao.Tratamento ?? await tratamentos.ObterPorId(sessao.TratamentoId), null);
            }

            if (!escolhido.HasValue)
            {
                return (null, null);
            }

            var tratamento = await tratamentos.ObterPorId(escolhido.Value);

            if (tratamento == null || tratamento.PacienteId != pacienteId)
            {
                return (null, "O tratamento informado não pertence a este paciente.");
            }

            // Um vínculo novo só com um tratamento que ainda está acontecendo.
            if (escolhido != atual && !StatusTratamento.Ativo(tratamento.Status))
            {
                return (null, ehOrigem
                    ? $"O tratamento {tratamento.Nome} está {tratamento.Status.ToLowerInvariant()}. " +
                      "Receitas e pedidos de exame são emitidos em tratamentos planejados ou em andamento."
                    : $"O tratamento {tratamento.Nome} está {tratamento.Status.ToLowerInvariant()}. " +
                      "Escolha um tratamento planejado ou em andamento, ou deixe sem tratamento.");
            }

            return (tratamento, null);
        }
    }

    /// <summary>
    /// De onde vem uma receita ou um pedido de exame: de uma <b>avaliação</b> ou de um
    /// <b>tratamento</b>. O que foi emitido no registro de uma sessão conta como do tratamento da
    /// sessão: a sessão fica guardada no registro, mas não é mostrada como origem.
    /// </summary>
    public static class OrigemClinica
    {
        public const string Avaliacao = "Avaliação";
        public const string Tratamento = "Tratamento";

        /// <summary>"Avaliação", "Tratamento" ou vazio (receitas antigas, sem origem).</summary>
        public static string Tipo(Guid? avaliacaoId, Guid? atendimentoId, Guid? tratamentoId)
        {
            if (avaliacaoId.HasValue && !atendimentoId.HasValue)
            {
                return Avaliacao;
            }

            return atendimentoId.HasValue || tratamentoId.HasValue ? Tratamento : string.Empty;
        }

        /// <summary>"Avaliação de 12/04/2026" ou "Tratamento Hidroterapia"; vazio sem origem.</summary>
        public static string Descrever(string tipo, AvaliacaoClinica? avaliacao, Models.Tratamento? tratamento)
        {
            if (tipo == Avaliacao && avaliacao != null)
            {
                return $"Avaliação de {RelogioDaClinica.Padrao.Formatar(avaliacao.DataRegistro, "dd/MM/yyyy")}";
            }

            return tipo == Tratamento && tratamento != null ? $"Tratamento {tratamento.Nome}" : string.Empty;
        }

        /// <summary>
        /// Texto da auditoria, que guarda também a sessão: "a avaliação de 12/04/2026",
        /// "o tratamento Hidroterapia" ou "a sessão nº 3 de 20/04/2026 do tratamento Hidroterapia".
        /// </summary>
        public static string ParaAuditoria(AvaliacaoClinica? avaliacao, AtendimentoFisioterapeutico? sessao, Models.Tratamento? tratamento)
        {
            if (sessao != null)
            {
                return $"a sessão nº {sessao.NumeroSessao} de " +
                       $"{RelogioDaClinica.Padrao.Formatar(sessao.DataRegistro, "dd/MM/yyyy")}" +
                       (tratamento != null ? $" do tratamento {tratamento.Nome}" : string.Empty);
            }

            if (avaliacao != null)
            {
                return $"a avaliação de {RelogioDaClinica.Padrao.Formatar(avaliacao.DataRegistro, "dd/MM/yyyy")}" +
                       (tratamento != null ? $", para o tratamento {tratamento.Nome}" : string.Empty);
            }

            return tratamento != null ? $"o tratamento {tratamento.Nome}" : "origem não informada";
        }
    }
}
