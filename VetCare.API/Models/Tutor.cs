namespace VetCare.API.Models
{
    public class Tutor
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;

        public Usuario? Usuario { get; set; }
        public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    }
}
