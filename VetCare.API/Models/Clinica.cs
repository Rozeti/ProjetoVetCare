namespace VetCare.API.Models
{
    /// <summary>
    /// Tenant da plataforma. A separação lógica por clínica prepara a evolução
    /// SaaS/Multi-Tenant descrita na Visão de Dados do DAS.
    /// </summary>
    public class Clinica
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;

        /// <summary>RN-009: antecedência mínima, em horas, para o tutor cancelar uma sessão.</summary>
        public int HorasMinimasCancelamento { get; set; } = 12;

        /// <summary>Início do expediente considerado na validação de agenda (HU-004).</summary>
        public TimeSpan HorarioAbertura { get; set; } = new TimeSpan(8, 0, 0);
        public TimeSpan HorarioFechamento { get; set; } = new TimeSpan(18, 0, 0);

        /// <summary>Duração padrão de uma sessão, usada para detectar sobreposição (RN-002).</summary>
        public int DuracaoSessaoMinutos { get; set; } = 60;

        public bool Ativa { get; set; } = true;
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    }
}
