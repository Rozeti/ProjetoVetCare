namespace VetCare.API.Models
{
    public class Tratamento
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PacienteId { get; set; }
        public Guid VeterinarioId { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string ObjetivoTerapeutico { get; set; } = string.Empty;

        /// <summary>Em Andamento, Concluído ou Interrompido.</summary>
        public string Status { get; set; } = "Em Andamento";
        public string ObservacoesGerais { get; set; } = string.Empty;

        public Pet? Paciente { get; set; }
        public Veterinario? Veterinario { get; set; }
        public ICollection<Sessao> Sessoes { get; set; } = new List<Sessao>();
    }
}
