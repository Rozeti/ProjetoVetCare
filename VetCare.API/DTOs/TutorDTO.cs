namespace VetCare.API.DTOs
{
    public class TutorDTO
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public int QuantidadePets { get; set; }

        /// <summary>Animais vinculados ao tutor.</summary>
        public List<PetDoTutorDTO> Pets { get; set; } = new();
    }

    public class PetDoTutorDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public bool Ativo { get; set; }
    }
}
