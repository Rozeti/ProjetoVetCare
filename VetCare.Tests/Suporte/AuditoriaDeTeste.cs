using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Security;
using VetCare.API.Services;
using VetCare.API.Services.Email;
using VetCare.API.Services.Push;

namespace VetCare.Tests.Suporte
{
    /// <summary>
    /// Fábricas dos serviços de apoio que os casos de uso exigem, mas cujo
    /// comportamento não é o objeto do teste.
    /// </summary>
    public static class Dependencias
    {
        /// <summary>Chave fixa, longa o bastante para o assinador de arquivos e o JWT.</summary>
        public const string ChaveDeTeste = "chave-de-teste-para-o-vetcare-com-mais-de-32-bytes";

        /// <summary>
        /// O acessor é criado sem definir o HttpContext de propósito. O
        /// <see cref="HttpContextAccessor"/> guarda o contexto num AsyncLocal estático
        /// compartilhado por todas as instâncias: atribuir um contexto novo aqui
        /// apagaria as claims que o <see cref="UsuarioAtual"/> do teste acabou de
        /// publicar. Deixando em branco, ambos leem o mesmo contexto ambiente — que é
        /// exatamente o que acontece em produção, com um contexto por requisição.
        /// </summary>
        public static AuditoriaService Auditoria(AppDbContext contexto, UsuarioAtual usuarioAtual) =>
            new(new AuditoriaRepository(contexto),
                usuarioAtual,
                new HttpContextAccessor(),
                NullLogger<AuditoriaService>.Instance);

        public static NotificacaoService Notificacoes(AppDbContext contexto) =>
            new(new NotificacaoRepository(contexto), new SinalDeNotificacoes());

        public static AssinadorDeArquivos Assinador() => new(Configuracao());

        public static IConfiguration Configuracao() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Chave"] = ChaveDeTeste })
                .Build();

        public static ModelosDeEmail ModelosDeEmail() =>
            new(Options.Create(new OpcoesDaAplicacao { UrlPortal = "http://portal.teste" }));

        /// <summary>
        /// Serviço de contas ligado a uma fila que o teste pode inspecionar, em ambiente
        /// "Development" e sem SMTP — ou seja, as credenciais voltam na resposta.
        /// </summary>
        public static ContasService Contas(AppDbContext contexto, FilaDeEmails fila, string ambiente = "Development") =>
            new(new TokenRedefinicaoRepository(contexto),
                fila,
                ModelosDeEmail(),
                Options.Create(new OpcoesDeEmail()),
                new AmbienteDeTeste(ambiente));

        private sealed class AmbienteDeTeste : IWebHostEnvironment
        {
            public AmbienteDeTeste(string nome) => EnvironmentName = nome;

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; } = "VetCare.Tests";
            public string WebRootPath { get; set; } = string.Empty;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }

    /// <summary>Canal de e-mail que apenas guarda o que foi enviado, para o teste conferir.</summary>
    public sealed class EmailDeTeste : IServicoDeEmail
    {
        public List<MensagemDeEmail> Enviados { get; } = new();

        /// <summary>Quando definido, cada envio falha com esta mensagem.</summary>
        public string? FalharCom { get; set; }

        public string Descricao => "e-mail de teste";

        /// <summary>Por padrão se comporta como um servidor real; o teste desliga para simular a caixa de saída local.</summary>
        public bool EnviaDeVerdade { get; set; } = true;

        public Task Enviar(MensagemDeEmail mensagem, CancellationToken cancelamento)
        {
            if (FalharCom != null)
            {
                throw new InvalidOperationException(FalharCom);
            }

            Enviados.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    /// <summary>Serviço de push que registra as mensagens e responde o que o teste mandar.</summary>
    public sealed class PushDeTeste : IServicoDePush
    {
        public List<MensagemPush> Enviadas { get; } = new();

        /// <summary>Tokens que devem ser tratados como aparelhos inexistentes.</summary>
        public HashSet<string> TokensInvalidos { get; } = new();

        /// <summary>Quando verdadeiro, todo envio falha de forma passageira.</summary>
        public bool FalhaPassageira { get; set; }

        public bool Habilitado { get; set; } = true;

        public Task<IReadOnlyList<ResultadoPush>> Enviar(IReadOnlyList<MensagemPush> mensagens, CancellationToken cancelamento)
        {
            Enviadas.AddRange(mensagens);

            IReadOnlyList<ResultadoPush> resultados = mensagens.Select(m =>
                TokensInvalidos.Contains(m.TokenPush)
                    ? new ResultadoPush(m.TokenPush, false, "DeviceNotRegistered", true)
                    : FalhaPassageira
                        ? new ResultadoPush(m.TokenPush, false, "Serviço indisponível", false)
                        : new ResultadoPush(m.TokenPush, true, null, false)).ToList();

            return Task.FromResult(resultados);
        }
    }
}
