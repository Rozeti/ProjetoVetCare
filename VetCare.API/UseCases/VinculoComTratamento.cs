using VetCare.API.Data;
using VetCare.API.Models;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Vínculo de receitas e pedidos de exame com o tratamento para o qual foram feitos. Quando
    /// o registro nasce de uma sessão, o tratamento é o da própria sessão; quando nasce de uma
    /// avaliação, o vínculo é opcional e o tratamento precisa ser do mesmo animal e estar ativo.
    /// </summary>
    public static class VinculoComTratamento
    {
        public const string MensagemDaSessao =
            "Receitas e pedidos de exame feitos em uma sessão ficam ligados ao tratamento da própria sessão.";

        /// <summary>
        /// Devolve o tratamento do registro (ou nenhum) e, quando a escolha não vale, a mensagem
        /// que a recusa. <paramref name="atual"/> é o tratamento já ligado ao registro em uma
        /// correção: ele continua aceito mesmo que tenha sido concluído depois.
        /// </summary>
        public static async Task<(Tratamento? Tratamento, string? Erro)> Resolver(
            ITratamentoRepository tratamentos,
            Guid pacienteId,
            AtendimentoFisioterapeutico? sessao,
            Guid? escolhido,
            Guid? atual = null)
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
                return (null,
                    $"O tratamento {tratamento.Nome} está {tratamento.Status.ToLowerInvariant()}. " +
                    "Escolha um tratamento planejado ou em andamento, ou deixe sem tratamento.");
            }

            return (tratamento, null);
        }
    }
}
