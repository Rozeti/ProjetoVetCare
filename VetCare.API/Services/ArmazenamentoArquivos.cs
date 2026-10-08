namespace VetCare.API.Services
{
    /// <summary>
    /// Grava mídias e documentos em disco, numa pasta fora de qualquer área pública da
    /// API (<c>Arquivos:Pasta</c>, por padrão <c>arquivos/</c> na raiz da aplicação). O
    /// banco guarda apenas o caminho relativo; a entrega acontece pelo
    /// <c>ArquivosController</c>, com URL assinada. A troca pelo armazenamento externo
    /// previsto no DAS (Amazon S3 ou MinIO) fica restrita a esta classe.
    /// </summary>
    public class ArmazenamentoArquivos
    {
        public const string PastaPadrao = "arquivos";

        private readonly string _raiz;
        private readonly ILogger<ArmazenamentoArquivos> _logger;

        public ArmazenamentoArquivos(
            IWebHostEnvironment ambiente,
            IConfiguration configuracao,
            ILogger<ArmazenamentoArquivos> logger)
        {
            var pasta = configuracao["Arquivos:Pasta"];

            _raiz = string.IsNullOrWhiteSpace(pasta)
                ? Path.Combine(ambiente.ContentRootPath, PastaPadrao)
                : Path.IsPathRooted(pasta) ? pasta : Path.Combine(ambiente.ContentRootPath, pasta);

            _logger = logger;
        }

        public string Raiz => _raiz;

        /// <summary>Tamanho da coluna que guarda o nome original do arquivo.</summary>
        public const int TamanhoMaximoDoNome = 255;

        /// <summary>
        /// Nome de exibição vindo do aparelho de quem envia: só o nome (sem pasta, que alguns
        /// navegadores incluem) e dentro do limite da coluna, preservando a extensão.
        /// </summary>
        public static string NomeSeguro(string? nomeOriginal)
        {
            var nome = Path.GetFileName((nomeOriginal ?? string.Empty).Trim());

            if (nome.Length == 0)
            {
                return "arquivo";
            }

            if (nome.Length <= TamanhoMaximoDoNome)
            {
                return nome;
            }

            var extensao = Path.GetExtension(nome);
            var base_ = Path.GetFileNameWithoutExtension(nome);

            return base_[..Math.Max(1, TamanhoMaximoDoNome - extensao.Length)] + extensao;
        }

        /// <summary>Grava o arquivo e devolve o caminho relativo que fica no banco ("/uploads/{guid}.jpg").</summary>
        public async Task<string> Salvar(IFormFile arquivo, string subpasta)
        {
            var pasta = Path.Combine(_raiz, subpasta);
            Directory.CreateDirectory(pasta);

            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var nomeUnico = $"{Guid.NewGuid()}{extensao}";
            var caminhoCompleto = Path.Combine(pasta, nomeUnico);

            await using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }

            return $"/{subpasta}/{nomeUnico}";
        }

        /// <summary>Remove o arquivo físico. Falhas são registradas, mas não interrompem o fluxo.</summary>
        public void Remover(string caminhoRelativo)
        {
            var caminho = ResolverCaminhoFisico(caminhoRelativo);

            if (caminho == null)
            {
                return;
            }

            try
            {
                File.Delete(caminho);
            }
            catch (IOException excecao)
            {
                _logger.LogWarning(excecao, "Não foi possível remover o arquivo {Caminho}.", caminhoRelativo);
            }
        }

        /// <summary>Caminho absoluto do arquivo, ou nulo se não existir ou se o caminho tentar sair da pasta.</summary>
        public string? ResolverCaminhoFisico(string caminhoRelativo)
        {
            if (string.IsNullOrWhiteSpace(caminhoRelativo))
            {
                return null;
            }

            var relativo = caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var caminho = Path.GetFullPath(Path.Combine(_raiz, relativo));

            // Defesa contra "../": o caminho resolvido precisa continuar dentro da raiz.
            if (!caminho.StartsWith(Path.GetFullPath(_raiz) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return null;
            }

            return File.Exists(caminho) ? caminho : null;
        }
    }
}
