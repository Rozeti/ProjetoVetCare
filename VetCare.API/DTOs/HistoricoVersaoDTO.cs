namespace VetCare.API.DTOs
{
    /// <summary>RN-004: versão anterior arquivada de um registro clínico.</summary>
    public class HistoricoVersaoDTO
    {
        public Guid Id { get; set; }
        public string TipoRegistro { get; set; } = string.Empty;
        public Guid RegistroId { get; set; }
        public string ConteudoAnterior { get; set; } = string.Empty;
        public string AlteradoPor { get; set; } = string.Empty;
        public DateTime DataAlteracao { get; set; }
    }
}
