namespace VetCare.API.DTOs
{
    /// <summary>HU-002, CA-4: desativação impede novos acessos sem excluir dados associados.</summary>
    public class AlterarStatusUsuarioDTO
    {
        public bool Ativo { get; set; }
    }
}
