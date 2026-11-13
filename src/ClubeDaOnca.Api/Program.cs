using System.Security.Claims;
using System.Text;
using ClubeDaOnca.Api.Endpoints;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Comunicacao;
using ClubeDaOnca.Api.Infrastructure.Contratos;
using ClubeDaOnca.Api.Infrastructure.Email;
using ClubeDaOnca.Api.Infrastructure.Pagamentos;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Hangfire;
using Hangfire.PostgreSql;
using ClubeDaOnca.Domain.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantAtual>();
builder.Services.AddScoped<UsuarioAtual>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentityCore<Usuario>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.ClaimsIdentity.UserIdClaimType = "sub"; // GetUserAsync lê a claim "sub" do JWT
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.Secao);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Chave.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Chave com pelo menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave)),
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Gestao", p => p.RequireRole(Perfis.Gestao))
    .AddPolicy("Secretaria", p => p.RequireRole(Perfis.Secretaria, Perfis.Admin))
    .AddPolicy("Admin", p => p.RequireRole(Perfis.Admin))
    .AddPolicy("SuperAdmin", p => p.RequireRole(Perfis.SuperAdmin))
    .AddPolicy("AdminOuSuperAdmin", p => p.RequireRole(Perfis.Admin, Perfis.SuperAdmin));

var carteirinha = builder.Configuration.GetSection(CarteirinhaOptions.Secao).Get<CarteirinhaOptions>() ?? new CarteirinhaOptions();
builder.Services.AddSingleton(ChavesCarteirinha.Criar(carteirinha));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RelogioClube>();

// Enums trafegam como texto ("Ativo", "Titular"...) nas respostas.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<FinanceiroService>();
builder.Services.AddScoped<EmissaoBoletos>();
builder.Services.AddScoped<ClubeDaOnca.Api.Infrastructure.Eventos.EventoService>();

// Comunicação: central de avisos, e-mail e WhatsApp (API oficial da Meta).
builder.Services.AddScoped<DivulgacaoService>();
builder.Services.AddScoped<JobsComunicacao>();
var whatsAppSecao = builder.Configuration.GetSection(WhatsAppOptions.Secao);
builder.Services.Configure<WhatsAppOptions>(whatsAppSecao);
if (string.Equals(whatsAppSecao["Modo"], "Memoria", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<WhatsAppMemoria>();
    builder.Services.AddSingleton<IEnvioWhatsApp>(sp => sp.GetRequiredService<WhatsAppMemoria>());
}
else
{
    builder.Services.AddHttpClient<IEnvioWhatsApp, WhatsAppMeta>(c => c.Timeout = TimeSpan.FromSeconds(20));
}
builder.Services.AddScoped<ContaAsaas>();

// Chaves de criptografia (protegem as chaves do Asaas de cada unidade) guardadas no próprio banco.
builder.Services.AddDataProtection()
    .SetApplicationName("ClubeDaOnca")
    .PersistKeysToDbContext<AppDbContext>();
builder.Services.AddScoped<JobsFinanceiro>();

// Contratos de adesão: documento (modelo .docx + PDF pelo LibreOffice) e e-mail da proposta.
builder.Services.AddSingleton<ConversorPdf>();
builder.Services.AddSingleton<GeradorContrato>();
builder.Services.AddScoped<ContratoService>();
builder.Services.AddScoped<JobsContratos>();

var emailSecao = builder.Configuration.GetSection(EmailOptions.Secao);
builder.Services.Configure<EmailOptions>(emailSecao);
var emailModo = emailSecao.Get<EmailOptions>()?.Modo ?? "Arquivo";
switch (emailModo)
{
    case "Smtp":
        builder.Services.AddSingleton<IEnvioEmail, EmailSmtp>();
        break;
    case "Memoria":
        builder.Services.AddSingleton<EmailMemoria>();
        builder.Services.AddSingleton<IEnvioEmail>(sp => sp.GetRequiredService<EmailMemoria>());
        break;
    case "Arquivo":
        builder.Services.AddSingleton<IEnvioEmail, EmailArquivo>();
        break;
    default:
        throw new InvalidOperationException($"Email:Modo desconhecido: {emailModo}. Use Smtp, Arquivo ou Memoria.");
}

// IP real do candidato no aceite quando a API roda atrás de proxy (Nginx, Traefik...).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Gateway de pagamento. O simulado só é aceito como padrão em desenvolvimento/testes.
var pagamentosSecao = builder.Configuration.GetSection(PagamentosOptions.Secao);
builder.Services.Configure<PagamentosOptions>(pagamentosSecao);
var pagamentos = pagamentosSecao.Get<PagamentosOptions>() ?? new PagamentosOptions();
var ambienteDeTeste = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var gatewayNome = !string.IsNullOrWhiteSpace(pagamentos.Gateway) ? pagamentos.Gateway
    : ambienteDeTeste ? "Simulado"
    : throw new InvalidOperationException("Configure Pagamentos:Gateway (Asaas) em produção.");
switch (gatewayNome)
{
    case "Simulado":
        builder.Services.AddSingleton<IGatewayPagamento, GatewaySimulado>();
        break;
    case "Asaas":
        // A conta geral é opcional: cada associação pode cadastrar a própria chave (Gestão → Associação → Pagamentos).
        builder.Services.AddHttpClient<IGatewayPagamento, AsaasGateway>(c => AsaasGateway.Configurar(c, pagamentos.Asaas));
        break;
    default:
        throw new InvalidOperationException($"Gateway de pagamento desconhecido: {gatewayNome}. Use Asaas ou Simulado.");
}

// Tarefas agendadas (desligadas nos testes de integração).
var jobsHabilitados = builder.Configuration.GetValue("Jobs:Habilitado", true);
if (jobsHabilitados)
{
    builder.Services.AddHangfire(c => c
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Default"))));
    builder.Services.AddHangfireServer();
}
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "API do Clube da Onça";
    doc.Info.Description = "Associações (unidades), autenticação, sócios, carteirinha digital, portaria, reservas de espaços e financeiro.";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Infra");

// Logo do clube para as páginas públicas (ex.: aceite do contrato).
app.MapGet("/marca/logo.png", (HttpContext http) =>
{
    if (ClubeDaOnca.Api.Infrastructure.Marca.Logo is not { } logo) return Results.NotFound();
    http.Response.Headers.CacheControl = "public, max-age=86400";
    return Results.File(logo, "image/png");
}).AllowAnonymous().ExcludeFromDescription();
app.MapAuth();
app.MapAssociacoes();
app.MapSocios();
app.MapCarteirinha();
app.MapReservas();
app.MapFinanceiro();
app.MapContratos();
app.MapEventos();
app.MapComunicacao();

// Aplica migrations, cria perfis e o admin inicial (se configurado).
await Seed.ExecutarAsync(app.Services, app.Configuration);

if (jobsHabilitados)
{
    var fuso = TimeZoneInfo.FindSystemTimeZoneById(app.Configuration["Clube:FusoHorario"] ?? "America/Sao_Paulo");
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<JobsFinanceiro>(
        JobsFinanceiro.GerarMensalidades, j => j.GerarMensalidadesDoMesAsync(), "0 6 1 * *",
        new RecurringJobOptions { TimeZone = fuso });
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<JobsComunicacao>(
        JobsComunicacao.AvisarVencimentos, j => j.AvisarVencimentosAsync(), "0 8 * * *",
        new RecurringJobOptions { TimeZone = fuso });
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<JobsContratos>(
        JobsContratos.RenovarContratos, j => j.RenovarVencidosAsync(), "30 5 * * *",
        new RecurringJobOptions { TimeZone = fuso });

    // Painel dos jobs: só em desenvolvimento (por padrão aceita apenas acesso local).
    if (app.Environment.IsDevelopment())
        app.UseHangfireDashboard("/hangfire");
}

app.Logger.LogInformation("Gateway de pagamento: {Gateway}", gatewayNome);
app.Logger.LogInformation("E-mail: modo {Modo}. PDF dos contratos: {LibreOffice}", emailModo,
    app.Services.GetRequiredService<ConversorPdf>().Executavel ?? "LibreOffice não encontrado (será enviado o .docx)");

app.Run();

public partial class Program;
