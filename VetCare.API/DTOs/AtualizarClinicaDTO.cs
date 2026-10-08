using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    public class AtualizarClinicaDTO
    {
        [Required(ErrorMessage = "O nome da clínica é obrigatório.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome da clínica deve ter entre 2 e 150 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "O CNPJ deve ter até 20 caracteres.")]
        public string Cnpj { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "O telefone deve ter até 30 caracteres.")]
        public string Telefone { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "O endereço deve ter até 250 caracteres.")]
        public string Endereco { get; set; } = string.Empty;

        [Range(0, 168, ErrorMessage = "A antecedência de cancelamento deve ficar entre 0 e 168 horas.")]
        public int HorasMinimasCancelamento { get; set; }

        [Range(15, 240, ErrorMessage = "A duração da sessão deve ficar entre 15 e 240 minutos.")]
        public int DuracaoSessaoMinutos { get; set; }

        [Required(ErrorMessage = "Informe o horário de abertura.")]
        public string HorarioAbertura { get; set; } = "08:00";

        [Required(ErrorMessage = "Informe o horário de fechamento.")]
        public string HorarioFechamento { get; set; } = "18:00";
    }
}
