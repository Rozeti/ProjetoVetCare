namespace VetCare.API.Models
{
    /// <summary>Receituário emitido pelo veterinário, vinculado ao prontuário do paciente.</summary>
    public class Prescricao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProntuarioId { get; set; }
        public Guid PacienteId { get; set; }
        public Guid VeterinarioId { get; set; }
        public Guid? AtendimentoId { get; set; }

        public DateTime DataEmissao { get; set; } = DateTime.UtcNow;
        public DateTime? ValidaAte { get; set; }
        public string Orientacoes { get; set; } = string.Empty;

        /// <summary>Ativa ou Cancelada. Receitas não são excluídas (RN-004).</summary>
        public string Status { get; set; } = "Ativa";

        public Prontuario? Prontuario { get; set; }
        public Pet? Paciente { get; set; }
        public Veterinario? Veterinario { get; set; }
        public ICollection<ItemPrescricao> Itens { get; set; } = new List<ItemPrescricao>();
    }

    public class ItemPrescricao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PrescricaoId { get; set; }
        public string Medicamento { get; set; } = string.Empty;
        public string Dosagem { get; set; } = string.Empty;
        public string Frequencia { get; set; } = string.Empty;
        public string Duracao { get; set; } = string.Empty;
        public string Via { get; set; } = string.Empty;
        public string Observacao { get; set; } = string.Empty;

        public Prescricao? Prescricao { get; set; }
    }
}
