using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-017: relatório de produtividade por período, com total de atendimentos por
    /// veterinário e o ranking das técnicas mais aplicadas.
    /// </summary>
    public class GerarRelatorioProdutividadeUseCase
    {
        private readonly IAtendimentoRepository _atendimentos;
        private readonly IAvaliacaoRepository _avaliacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public GerarRelatorioProdutividadeUseCase(
            IAtendimentoRepository atendimentos,
            IAvaliacaoRepository avaliacoes,
            UsuarioAtual usuarioAtual)
        {
            _atendimentos = atendimentos;
            _avaliacoes = avaliacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<RelatorioProdutividadeDTO>> Executar(DateTime inicio, DateTime fim)
        {
            if (fim < inicio)
            {
                return Resultado<RelatorioProdutividadeDTO>.Invalido(
                    "A data final do período deve ser igual ou posterior à data inicial.");
            }

            // O intervalo é fechado no início e aberto no fim, incluindo o dia final por inteiro.
            var inicioUtc = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Local).ToUniversalTime();
            var fimUtc = DateTime.SpecifyKind(fim.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            // RN-008: o veterinário vê apenas a própria produtividade.
            var filtroVeterinario = _usuarioAtual.EhVeterinario ? _usuarioAtual.VeterinarioId : null;

            var atendimentos = await _atendimentos.ObterPorPeriodo(
                _usuarioAtual.ClinicaId, filtroVeterinario, inicioUtc, fimUtc);

            var totalAvaliacoes = await _avaliacoes.ContarPorPeriodo(
                _usuarioAtual.ClinicaId, filtroVeterinario, inicioUtc, fimUtc);

            var relatorio = new RelatorioProdutividadeDTO
            {
                Inicio = inicio.Date,
                Fim = fim.Date,
                TotalAtendimentos = atendimentos.Count,
                TotalAvaliacoes = totalAvaliacoes,
                MediaEscalaDor = atendimentos.Count == 0
                    ? 0
                    : Math.Round((decimal)atendimentos.Average(a => a.EscalaDor), 1)
            };

            // HU-017, CA-1: total de atendimentos por veterinário no intervalo.
            relatorio.PorVeterinario = atendimentos
                .GroupBy(a => new { a.VeterinarioId, Nome = a.Veterinario?.Usuario?.Nome ?? "Não identificado" })
                .Select(grupo => new AtendimentosPorVeterinarioDTO
                {
                    VeterinarioId = grupo.Key.VeterinarioId,
                    Nome = grupo.Key.Nome,
                    TotalAtendimentos = grupo.Count(),
                    MediaEscalaDor = Math.Round((decimal)grupo.Average(a => a.EscalaDor), 1)
                })
                .OrderByDescending(v => v.TotalAtendimentos)
                .ToList();

            // HU-017, CA-2: ranking das técnicas mais aplicadas no período.
            relatorio.TecnicasMaisAplicadas = atendimentos
                .SelectMany(a => SepararTecnicas(a.TecnicasAplicadas))
                .GroupBy(tecnica => tecnica, StringComparer.OrdinalIgnoreCase)
                .Select(grupo => new TecnicaAplicadaDTO
                {
                    Tecnica = grupo.Key,
                    Ocorrencias = grupo.Count()
                })
                .OrderByDescending(t => t.Ocorrencias)
                .ThenBy(t => t.Tecnica)
                .Take(15)
                .ToList();

            return Resultado<RelatorioProdutividadeDTO>.Ok(relatorio);
        }

        /// <summary>
        /// As técnicas são digitadas em texto livre, separadas por vírgula ou ponto e vírgula.
        /// A quebra permite montar o ranking sem exigir um cadastro prévio de técnicas.
        /// </summary>
        private static IEnumerable<string> SepararTecnicas(string tecnicasAplicadas)
        {
            if (string.IsNullOrWhiteSpace(tecnicasAplicadas))
            {
                return Array.Empty<string>();
            }

            return tecnicasAplicadas
                .Split(new[] { ',', ';', '/', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 1)
                .Select(NormalizarCaixa);
        }

        private static string NormalizarCaixa(string texto)
        {
            return char.ToUpperInvariant(texto[0]) + texto[1..].ToLowerInvariant();
        }
    }
}
