namespace VetCare.API.DTOs
{
    /// <summary>HU-017: relatório de produtividade por período.</summary>
    public class RelatorioProdutividadeDTO
    {
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
        public int TotalAtendimentos { get; set; }
        public int TotalAvaliacoes { get; set; }
        public decimal MediaEscalaDor { get; set; }

        /// <summary>HU-017, CA-3: indica período sem registros para a interface avisar o usuário.</summary>
        public bool SemRegistros => TotalAtendimentos == 0 && TotalAvaliacoes == 0;

        public List<AtendimentosPorVeterinarioDTO> PorVeterinario { get; set; } = new();
        public List<TecnicaAplicadaDTO> TecnicasMaisAplicadas { get; set; } = new();
    }

    public class AtendimentosPorVeterinarioDTO
    {
        public Guid VeterinarioId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int TotalAtendimentos { get; set; }
        public decimal MediaEscalaDor { get; set; }
    }

    public class TecnicaAplicadaDTO
    {
        public string Tecnica { get; set; } = string.Empty;
        public int Ocorrencias { get; set; }
    }
}
