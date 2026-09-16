namespace VetCare.API.Services
{
    /// <summary>
    /// Abstrai a gravação de mídias e documentos. Hoje persiste em disco sob wwwroot;
    /// o DAS prevê a troca por armazenamento externo (Amazon S3 ou MinIO) sem que os
    /// casos de uso precisem mudar — o banco guarda apenas os metadados e a URL.
    /// </summary>
    public class ArmazenamentoArquivos
    {
        private readonly IWebHostEnvironment _ambiente;
        private readonly ILogger<ArmazenamentoArquivos> _logger;

        public ArmazenamentoArquivos(IWebHostEnvironment ambiente, ILogger<ArmazenamentoArquivos> logger)
        {
            _ambiente = ambiente;
            _logger = logger;
        }

        /// <summary>Grava o arquivo e devolve a URL pública relativa.</summary>
        public async Task<string> Salvar(IFormFile arquivo, string subpasta)
        {
            var raiz = ObterRaiz();
            var pasta = Path.Combine(raiz, subpasta);

            if (!Directory.Exists(pasta))
            {
                Directory.CreateDirectory(pasta);
            }

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
        public void Remover(string urlRelativa)
        {
            if (string.IsNullOrWhiteSpace(urlRelativa))
            {
                return;
            }

            try
            {
                var caminho = Path.Combine(ObterRaiz(), urlRelativa.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(caminho))
                {
                    File.Delete(caminho);
                }
            }
            catch (IOException excecao)
            {
                _logger.LogWarning(excecao, "Não foi possível remover o arquivo {Url}.", urlRelativa);
            }
        }

        public string? ResolverCaminhoFisico(string urlRelativa)
        {
            if (string.IsNullOrWhiteSpace(urlRelativa))
            {
                return null;
            }

            var caminho = Path.Combine(ObterRaiz(), urlRelativa.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(caminho) ? caminho : null;
        }

        private string ObterRaiz()
        {
            var raiz = _ambiente.WebRootPath;

            if (string.IsNullOrWhiteSpace(raiz))
            {
                raiz = Path.Combine(_ambiente.ContentRootPath, "wwwroot");
                Directory.CreateDirectory(raiz);
            }

            return raiz;
        }
    }
}
