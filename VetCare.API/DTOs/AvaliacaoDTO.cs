namespace VetCare.API.DTOs
{
    public class AvaliacaoDTO
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public Guid ProntuarioId { get; set; }
        public DateTime DataRegistro { get; set; }
        public DateTime? DataUltimaEdicao { get; set; }
        public Guid VeterinarioId { get; set; }
        public string NomeVeterinario { get; set; } = string.Empty;
        public string MotivoConsulta { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string DiagnosticoPossivel { get; set; } = string.Empty;

        /// <summary>Tratamentos encaminhados a partir desta avaliação (pode ser nenhum).</summary>
        public List<TratamentoResumoDTO> Tratamentos { get; set; } = new();

        public int QuantidadeReceitas { get; set; }
        public int QuantidadePedidosExame { get; set; }
    }

    public class TratamentoResumoDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
