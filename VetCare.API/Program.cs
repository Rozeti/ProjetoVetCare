using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.Security;
using VetCare.API.Services;
using VetCare.API.Services.Email;
using VetCare.API.Services.Push;
using VetCare.API.UseCases;

var builder = WebApplication.CreateBuilder(args);

// O filtro global anuncia toda escrita bem-sucedida no mural de atualizações, o que
// mantém as telas abertas em sincronia sem cada caso de uso ter de se preocupar com isso.
builder.Services.AddControllers(opcoes =>
{
    opcoes.Filters.Add<FiltroDeAtualizacoes>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
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

// Seções de configuração: endereço do portal e fuso da clínica, e-mail e push.
builder.Services.Configure<OpcoesDaAplicacao>(builder.Configuration.GetSection(OpcoesDaAplicacao.Secao));
builder.Services.Configure<OpcoesDeEmail>(builder.Configuration.GetSection(OpcoesDeEmail.Secao));
builder.Services.Configure<OpcoesDePush>(builder.Configuration.GetSection(OpcoesDePush.Secao));

// Horários de expediente, "dia de hoje" e datas nos e-mails seguem o fuso da clínica,
// não o do servidor — um contêiner roda em UTC.
RelogioDaClinica.Configurar(builder.Configuration["Aplicacao:FusoHorario"]);
builder.Services.AddSingleton(RelogioDaClinica.Padrao);

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
AdicionarServicos(builder.Services, builder.Configuration);
AdicionarCasosDeUso(builder.Services);

builder.Services.AddHostedService<LembreteConfirmacaoService>();
builder.Services.AddHostedService<LembreteVacinacaoService>();
builder.Services.AddHostedService<ProcessadorDeEmails>();
builder.Services.AddHostedService<EntregadorDeNotificacoes>();

var chaveJwt = builder.Configuration["Jwt:Chave"];

if (string.IsNullOrWhiteSpace(chaveJwt) || Encoding.UTF8.GetByteCount(chaveJwt) < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt:Chave com pelo menos 32 bytes em appsettings.json ou na variável de ambiente Jwt__Chave.");
}

var emissorJwt = builder.Configuration["Jwt:Emissor"] is { Length: > 0 } emissor ? emissor : TokenService.EmissorPadrao;
var publicoJwt = builder.Configuration["Jwt:Publico"] is { Length: > 0 } publico ? publico : TokenService.EmissorPadrao;

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
        ValidateIssuer = true,
        ValidIssuer = emissorJwt,
        ValidateAudience = true,
        ValidAudience = publicoJwt,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    x.Events = new JwtBearerEvents
    {
        // HU-002, CA-4: a conta desativada perde o acesso na hora, mesmo com um token ainda
        // dentro do prazo. A consulta ao banco fica em cache por um minuto.
        OnTokenValidated = async contexto =>
        {
            var valor = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(valor, out var usuarioId))
            {
                contexto.Fail("Token sem identificação do usuário.");
                return;
            }

            var contas = contexto.HttpContext.RequestServices.GetRequiredService<ContasAtivas>();

            var ativa = await contas.EstaAtiva(usuarioId, async () =>
            {
                var usuarios = contexto.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
                var usuario = await usuarios.ObterPorId(usuarioId);
                return usuario is { Ativo: true };
            });

            if (!ativa)
            {
                contexto.Fail("Esta conta está inativa.");
            }
        }
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
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var esperar))
        {
            contexto.HttpContext.Response.Headers.RetryAfter = ((int)esperar.TotalSeconds).ToString();
        }

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

// A documentação interativa fica ligada em desenvolvimento e, fora dele, só quando
// Documentacao:Habilitada for true — sem precisar mudar o ambiente inteiro para isso.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Documentacao:Habilitada"))
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.Title = "VetCare — API";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

// O redirecionamento para HTTPS só faz sentido quando a própria API escuta em HTTPS. Atrás
// de um proxy reverso com certificado (o cenário do docker-compose) ele fica desligado.
if (!app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seguranca:RedirecionarParaHttps"))
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Mídias e documentos não são servidos como arquivos estáticos: saem pelo ArquivosController,
// com URL assinada (RN-003, RNF-002).
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

RegistrarResumoDaInicializacao(app);

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
    servicos.AddScoped<IDispositivoRepository, DispositivoRepository>();
}

static void AdicionarServicos(IServiceCollection servicos, IConfiguration configuracao)
{
    servicos.AddScoped<TokenService>();
    servicos.AddSingleton<PasswordHasher>();
    servicos.AddSingleton<ContasAtivas>();
    servicos.AddSingleton<AssinadorDeArquivos>();
    servicos.AddScoped<UsuarioAtual>();
    servicos.AddScoped<ArmazenamentoArquivos>();
    servicos.AddScoped<NotificacaoService>();
    servicos.AddScoped<AuditoriaService>();
    servicos.AddScoped<ContasService>();

    // Singleton: o mural precisa ser o mesmo para todas as requisições da aplicação.
    servicos.AddSingleton<CentralDeAtualizacoes>();

    // E-mail: SMTP quando configurado; caso contrário os e-mails vão para uma pasta local,
    // o que basta para desenvolver e demonstrar o fluxo sem conta de e-mail.
    var email = configuracao.GetSection(OpcoesDeEmail.Secao).Get<OpcoesDeEmail>() ?? new OpcoesDeEmail();

    if (email.SmtpConfigurado)
    {
        servicos.AddSingleton<IServicoDeEmail, ServicoDeEmailSmtp>();
    }
    else
    {
        servicos.AddSingleton<IServicoDeEmail, CaixaDeSaidaLocal>();
    }

    servicos.AddSingleton<ModelosDeEmail>();
    servicos.AddSingleton<FilaDeEmails>();

    // Situação do canal de e-mail e envio de teste, para o administrador conferir a configuração.
    servicos.AddScoped<DiagnosticoDeEmail>();

    // Push: o serviço da Expo entrega no celular do tutor; o sinal acorda o entregador.
    servicos.AddSingleton<SinalDeNotificacoes>();
    servicos.AddSingleton<IServicoDePush, ServicoDePushExpo>();

    servicos.AddHttpClient(ServicoDePushExpo.NomeDoCliente, cliente =>
    {
        cliente.Timeout = TimeSpan.FromSeconds(15);
        cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    });
}

static void AdicionarCasosDeUso(IServiceCollection servicos)
{
    servicos.AddScoped<AutenticarUsuarioUseCase>();
    servicos.AddScoped<RecuperarSenhaUseCase>();
    servicos.AddScoped<GerenciarUsuariosUseCase>();
    servicos.AddScoped<GerenciarTutoresUseCase>();
    servicos.AddScoped<GerenciarVeterinariosUseCase>();
    servicos.AddScoped<GerenciarClinicaUseCase>();
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
    servicos.AddScoped<DispositivosUseCase>();
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

// Deixa claro no log, logo na subida, como e-mail, push e fuso estão configurados.
static void RegistrarResumoDaInicializacao(WebApplication aplicacao)
{
    var logger = aplicacao.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Inicializacao");
    var email = aplicacao.Services.GetRequiredService<IServicoDeEmail>();
    var push = aplicacao.Services.GetRequiredService<IServicoDePush>();
    var relogio = aplicacao.Services.GetRequiredService<RelogioDaClinica>();
    var portal = aplicacao.Configuration["Aplicacao:UrlPortal"] ?? "http://localhost:5173";

    logger.LogInformation("E-mails: {Canal}.", email.Descricao);
    logger.LogInformation("Notificações no celular: {Estado}.", push.Habilitado ? "ativas (Expo Push)" : "desligadas");
    logger.LogInformation("Fuso da clínica: {Fuso}. Portal: {Portal}.", relogio.Fuso.Id, portal);
}

/// <summary>Exposto para que o projeto de testes possa instanciar a aplicação.</summary>
public partial class Program;
