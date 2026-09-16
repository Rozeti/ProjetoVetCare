using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Security;
using VetCare.API.Services;
using VetCare.API.UseCases;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratamentoDeErros>();

// Erros de validação saem no mesmo formato { mensagem } usado pelos casos de uso,
// para que o front trate todas as respostas de erro de uma única maneira.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = contexto =>
    {
        var mensagem = contexto.ModelState
            .SelectMany(estado => estado.Value?.Errors ?? new Microsoft.AspNetCore.Mvc.ModelBinding.ModelErrorCollection())
            .Select(erro => erro.ErrorMessage)
            .FirstOrDefault(texto => !string.IsNullOrWhiteSpace(texto))
            ?? "Dados inválidos na requisição.";

        return new BadRequestObjectResult(new { mensagem });
    };
});

// Cada entrada pode trazer vários endereços separados por vírgula. Isso permite liberar, por
// exemplo, o portal em localhost e no IP da rede local numa única variável de ambiente, que é
// como o docker-compose entrega a configuração.
var origensPermitidas = (builder.Configuration
        .GetSection("Cors:OrigensPermitidas")
        .Get<string[]>() ?? new[] { "http://localhost:5173" })
    .SelectMany(entrada => entrada.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.WithOrigins(origensPermitidas).AllowAnyHeader().AllowAnyMethod();
    });

    // O aplicativo roda em dispositivo físico ou emulador, com origem variável.
    options.AddPolicy("PermitirMobile", policy =>
    {
        policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// RNF-005: o monitoramento precisa distinguir a aplicação no ar do banco acessível.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("banco-de-dados", tags: new[] { "pronto" });

AdicionarRepositorios(builder.Services);
AdicionarServicos(builder.Services);
AdicionarCasosDeUso(builder.Services);

builder.Services.AddHostedService<LembreteConfirmacaoService>();
builder.Services.AddHostedService<LembreteVacinacaoService>();

var chaveJwt = builder.Configuration["Jwt:Chave"];

if (string.IsNullOrWhiteSpace(chaveJwt) || Encoding.UTF8.GetByteCount(chaveJwt) < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt:Chave com pelo menos 32 bytes em appsettings.json ou na variável de ambiente Jwt__Chave.");
}

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// Complementa a RN-006: o bloqueio por tentativas protege uma conta; o limite por
// endereço barra a varredura de várias contas a partir da mesma origem.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (contexto, _) =>
    {
        await contexto.HttpContext.Response.WriteAsJsonAsync(new
        {
            mensagem = "Muitas tentativas em pouco tempo. Aguarde um instante e tente novamente."
        });
    };

    options.AddPolicy(LimitesDeRequisicao.Autenticacao, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy(LimitesDeRequisicao.Upload, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.Identity?.Name ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddOpenApi();

var app = builder.Build();

await PrepararBancoDeDados(app);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Documentação navegável da API, útil para quem integra e para a avaliação do projeto.
    app.MapScalarApiReference(options =>
    {
        options.Title = "VetCare — API";
        options.Theme = ScalarTheme.BluePlanet;
    });
}
else
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseStaticFiles();
app.UseCors(app.Environment.IsDevelopment() ? "PermitirMobile" : "PermitirFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Verificação superficial: a aplicação respondeu.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Verificação completa: a aplicação consegue falar com o banco.
app.MapHealthChecks("/health/pronto", new HealthCheckOptions
{
    Predicate = registro => registro.Tags.Contains("pronto"),
    ResponseWriter = async (contexto, relatorio) =>
    {
        contexto.Response.ContentType = "application/json";

        await contexto.Response.WriteAsJsonAsync(new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = relatorio.TotalDuration.TotalMilliseconds,
            verificacoes = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description
            })
        });
    }
}).AllowAnonymous();

app.Run();

static void AdicionarRepositorios(IServiceCollection servicos)
{
    servicos.AddScoped<IClinicaRepository, ClinicaRepository>();
    servicos.AddScoped<IUsuarioRepository, UsuarioRepository>();
    servicos.AddScoped<ITutorRepository, TutorRepository>();
    servicos.AddScoped<IVeterinarioRepository, VeterinarioRepository>();
    servicos.AddScoped<IApoioRepository, ApoioRepository>();
    servicos.AddScoped<IPetRepository, PetRepository>();
    servicos.AddScoped<IAlergiaRepository, AlergiaRepository>();
    servicos.AddScoped<IVacinaRepository, VacinaRepository>();
    servicos.AddScoped<ITratamentoRepository, TratamentoRepository>();
    servicos.AddScoped<ISessaoRepository, SessaoRepository>();
    servicos.AddScoped<IBloqueioAgendaRepository, BloqueioAgendaRepository>();
    servicos.AddScoped<IProntuarioRepository, ProntuarioRepository>();
    servicos.AddScoped<IAvaliacaoRepository, AvaliacaoRepository>();
    servicos.AddScoped<IAtendimentoRepository, AtendimentoRepository>();
    servicos.AddScoped<IPrescricaoRepository, PrescricaoRepository>();
    servicos.AddScoped<IMidiaRepository, MidiaRepository>();
    servicos.AddScoped<IObservacaoInternaRepository, ObservacaoInternaRepository>();
    servicos.AddScoped<IDocumentoRepository, DocumentoRepository>();
    servicos.AddScoped<IMensagemRepository, MensagemRepository>();
    servicos.AddScoped<INotificacaoRepository, NotificacaoRepository>();
    servicos.AddScoped<IVersaoRegistroRepository, VersaoRegistroRepository>();
    servicos.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
    servicos.AddScoped<ITokenRedefinicaoRepository, TokenRedefinicaoRepository>();
}

static void AdicionarServicos(IServiceCollection servicos)
{
    servicos.AddScoped<TokenService>();
    servicos.AddSingleton<PasswordHasher>();
    servicos.AddScoped<UsuarioAtual>();
    servicos.AddScoped<ArmazenamentoArquivos>();
    servicos.AddScoped<NotificacaoService>();
    servicos.AddScoped<AuditoriaService>();
}

static void AdicionarCasosDeUso(IServiceCollection servicos)
{
    servicos.AddScoped<AutenticarUsuarioUseCase>();
    servicos.AddScoped<RecuperarSenhaUseCase>();
    servicos.AddScoped<GerenciarUsuariosUseCase>();
    servicos.AddScoped<GerenciarTutoresUseCase>();
    servicos.AddScoped<GerenciarVeterinariosUseCase>();
    servicos.AddScoped<GerenciarPacientesUseCase>();
    servicos.AddScoped<GerenciarAlergiasUseCase>();
    servicos.AddScoped<GerenciarVacinasUseCase>();
    servicos.AddScoped<GerenciarTratamentosUseCase>();
    servicos.AddScoped<AgendarSessaoUseCase>();
    servicos.AddScoped<AtualizarStatusSessaoUseCase>();
    servicos.AddScoped<ConsultarAgendaUseCase>();
    servicos.AddScoped<GerenciarBloqueiosAgendaUseCase>();
    servicos.AddScoped<RegistrarAvaliacaoUseCase>();
    servicos.AddScoped<RegistrarAtendimentoUseCase>();
    servicos.AddScoped<RegistrarObservacaoInternaUseCase>();
    servicos.AddScoped<GerenciarPrescricoesUseCase>();
    servicos.AddScoped<AnexarMidiaUseCase>();
    servicos.AddScoped<GerenciarDocumentosUseCase>();
    servicos.AddScoped<ConsultarProntuarioUseCase>();
    servicos.AddScoped<MensagensUseCase>();
    servicos.AddScoped<NotificacoesUseCase>();
    servicos.AddScoped<ConsultarIndicadoresUseCase>();
    servicos.AddScoped<GerarRelatorioProdutividadeUseCase>();
    servicos.AddScoped<ConsultarAuditoriaUseCase>();
}

// Aplica as migrações pendentes e garante o acesso administrativo. Em contêiner o
// banco pode demorar alguns segundos para aceitar conexões, por isso há uma espera
// com novas tentativas antes de desistir.
static async Task PrepararBancoDeDados(WebApplication aplicacao)
{
    using var escopo = aplicacao.Services.CreateScope();

    var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Inicializacao");

    const int tentativasMaximas = 10;

    for (var tentativa = 1; tentativa <= tentativasMaximas; tentativa++)
    {
        try
        {
            await contexto.Database.MigrateAsync();
            await SeedInicial.Executar(aplicacao.Services);
            return;
        }
        catch (Exception excecao) when (tentativa < tentativasMaximas)
        {
            logger.LogWarning(
                "Banco de dados ainda indisponível ({Tentativa}/{Total}): {Erro}. Nova tentativa em 3 segundos.",
                tentativa, tentativasMaximas, excecao.Message);

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    logger.LogError(
        "Não foi possível preparar o banco de dados após {Total} tentativas. " +
        "Verifique se o PostgreSQL está acessível na connection string configurada.",
        tentativasMaximas);

    throw new InvalidOperationException("Banco de dados indisponível na inicialização da API.");
}

/// <summary>Exposto para que o projeto de testes possa instanciar a aplicação.</summary>
public partial class Program;
