using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarClinicaDTO
    {
        [Required(ErrorMessage = "O nome da clínica é obrigatório.")]
        public string Nome { get; set; } = string.Empty;

        public string Cnpj { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;

        [Range(0, 168, ErrorMessage = "A antecedência de cancelamento deve ficar entre 0 e 168 horas.")]
        public int HorasMinimasCancelamento { get; set; }

        [Range(15, 240, ErrorMessage = "A duração da sessão deve ficar entre 15 e 240 minutos.")]
        public int DuracaoSessaoMinutos { get; set; }

        public string HorarioAbertura { get; set; } = "08:00";
        public string HorarioFechamento { get; set; } = "18:00";
    }
}
