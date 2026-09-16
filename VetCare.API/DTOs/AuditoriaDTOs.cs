namespace VetCare.API.DTOs
{
    public class RegistroAuditoriaDTO
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string NomeUsuario { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
        public string Acao { get; set; } = string.Empty;
        public string Entidade { get; set; } = string.Empty;
        public Guid? EntidadeId { get; set; }
        public string Detalhe { get; set; } = string.Empty;
        public string EnderecoIp { get; set; } = string.Empty;
        public DateTime DataHora { get; set; }
    }
}
