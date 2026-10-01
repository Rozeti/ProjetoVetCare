using VetCare.API.Models;

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

        public static List<HistoricoVersaoDTO> MapearLista(IEnumerable<VersaoRegistroClinico> versoes)
        {
            return versoes.Select(v => new HistoricoVersaoDTO
            {
                Id = v.Id,
                TipoRegistro = v.TipoRegistro,
                RegistroId = v.RegistroId,
                ConteudoAnterior = v.ConteudoAnterior,
                AlteradoPor = v.AlteradoPor?.Nome ?? string.Empty,
                DataAlteracao = v.DataAlteracao
            }).ToList();
        }
    }
}
