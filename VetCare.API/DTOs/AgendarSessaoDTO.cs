using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AgendarSessaoDTO
    {
        [Required(ErrorMessage = "O tratamento é obrigatório.")]
        public Guid TratamentoId { get; set; }

        /// <summary>Opcional para o veterinário logado: assume o próprio vínculo quando ausente.</summary>
        public Guid? VeterinarioId { get; set; }

        [Required(ErrorMessage = "A data e a hora da sessão são obrigatórias.")]
        public DateTime DataHora { get; set; }

        public string Observacoes { get; set; } = string.Empty;
    }
}
