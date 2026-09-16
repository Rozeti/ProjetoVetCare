using Microsoft.EntityFrameworkCore;

namespace VetCare.API.Common
{
    /// <summary>
    /// Parâmetros de paginação aceitos pelas listagens. O limite máximo por página
    /// evita que uma requisição peça a base inteira e comprometa o tempo de resposta
    /// exigido pelo RNF-004.
    /// </summary>
    public class ParametrosPagina
    {
        private const int TamanhoMaximo = 100;
        private int _tamanho = 20;

        public int Pagina { get; set; } = 1;

        public int Tamanho
        {
            get => _tamanho;
            set => _tamanho = value is < 1 or > TamanhoMaximo ? TamanhoMaximo : value;
        }

        public int PaginaSegura => Pagina < 1 ? 1 : Pagina;

        public int Pular => (PaginaSegura - 1) * Tamanho;
    }

    public class PaginaDe<T>
    {
        public IReadOnlyList<T> Itens { get; init; } = Array.Empty<T>();
        public int Pagina { get; init; }
        public int Tamanho { get; init; }
        public int Total { get; init; }

        public int TotalDePaginas => Tamanho == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamanho);
        public bool TemAnterior => Pagina > 1;
        public bool TemProxima => Pagina < TotalDePaginas;

        public static PaginaDe<T> Criar(IReadOnlyList<T> itens, int pagina, int tamanho, int total) =>
            new() { Itens = itens, Pagina = pagina, Tamanho = tamanho, Total = total };

        public PaginaDe<TDestino> Converter<TDestino>(Func<T, TDestino> conversao) =>
            PaginaDe<TDestino>.Criar(Itens.Select(conversao).ToList(), Pagina, Tamanho, Total);
    }

    public static class ConsultaExtensions
    {
        /// <summary>
        /// Executa a contagem e a página em duas consultas, o que mantém o custo
        /// constante independentemente do tamanho da tabela.
        /// </summary>
        public static async Task<PaginaDe<T>> Paginar<T>(this IQueryable<T> consulta, ParametrosPagina parametros)
        {
            var total = await consulta.CountAsync();

            var itens = await consulta
                .Skip(parametros.Pular)
                .Take(parametros.Tamanho)
                .ToListAsync();

            return PaginaDe<T>.Criar(itens, parametros.PaginaSegura, parametros.Tamanho, total);
        }
    }
}
