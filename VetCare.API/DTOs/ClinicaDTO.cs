namespace VetCare.API.DTOs
{
    public class ClinicaDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;

        /// <summary>RN-009: antecedência mínima em horas para cancelamento pelo tutor.</summary>
        public int HorasMinimasCancelamento { get; set; }

        public string HorarioAbertura { get; set; } = string.Empty;
        public string HorarioFechamento { get; set; } = string.Empty;
        public int DuracaoSessaoMinutos { get; set; }
    }
}
