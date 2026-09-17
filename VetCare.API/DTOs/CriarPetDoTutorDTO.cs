using System.ComponentModel.DataAnnotations;

namespace VetCare.API.DTOs
{
    /// <summary>
    /// Cadastro de paciente feito pelo próprio tutor, no portal ou no aplicativo, quando
    /// ele adquire um animal que ainda não existe no sistema — sem precisar ir à clínica
    /// só para isso.
    ///
    /// Não existe campo de tutor: o vínculo exigido pela RN-001 é resolvido pela API a
    /// partir de quem está autenticado, de modo que ninguém consegue cadastrar um animal
    /// no nome de outra pessoa.
    /// </summary>
    public class CriarPetDoTutorDTO
    {
        [Required(ErrorMessage = "O nome do paciente é obrigatório.")]
        [StringLength(80, MinimumLength = 1)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "A espécie é obrigatória.")]
        public string Especie { get; set; } = string.Empty;

        public string Raca { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
        public DateTime DataNascimento { get; set; }

        public string Sexo { get; set; } = string.Empty;
        public string Pelagem { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public bool Castrado { get; set; }

        [Range(0.1, 200, ErrorMessage = "Informe um peso entre 0,1 e 200 kg.")]
        public decimal? PesoAtualKg { get; set; }
    }
}
