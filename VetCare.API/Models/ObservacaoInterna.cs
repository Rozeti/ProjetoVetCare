namespace VetCare.API.Models
{
    /// <summary>
    /// RN-003 / RNF-003: anotação clínica restrita aos perfis Administrador e Veterinário.
    /// Nunca deve ser exposta ao Tutor, nem mesmo parcialmente.
    /// </summary>
    public class ObservacaoInterna
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProntuarioId { get; set; }
        public Guid? AvaliacaoId { get; set; }
        public Guid? AtendimentoId { get; set; }
        public Guid AutorId { get; set; }
        public string Conteudo { get; set; } = string.Empty;
        public DateTime DataRegistro { get; set; } = DateTime.UtcNow;

        public Prontuario? Prontuario { get; set; }
        public AvaliacaoClinica? Avaliacao { get; set; }
        public AtendimentoFisioterapeutico? Atendimento { get; set; }
        public Usuario? Autor { get; set; }
    }
}
