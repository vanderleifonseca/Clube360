using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace ClubeDaOnca.Api.Tests;

/// <summary>Sobe a API real contra um Postgres descartável em container.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@teste.local";
    public const string AdminSenha = "Senha@1234";
    public const string WebhookToken = "token-webhook-de-teste";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:Chave", "chave-de-teste-com-pelo-menos-32-caracteres!");
        builder.UseSetting("Carteirinha:ChaveMestra", "chave-mestra-de-teste-com-mais-de-32-caracteres");
        builder.UseSetting("Jobs:Habilitado", "false");
        builder.UseSetting("Pagamentos:Gateway", "Simulado");
        builder.UseSetting("Email:Modo", "Memoria");
        builder.UseSetting("Comunicacao:WhatsApp:Modo", "Memoria");
        builder.UseSetting("Contratos:LibreOffice", "desligado");
        builder.UseSetting("Pagamentos:Asaas:WebhookToken", WebhookToken);
        builder.UseSetting("AdminInicial:Email", AdminEmail);
        builder.UseSetting("AdminInicial:Senha", AdminSenha);
    }

    public Task InitializeAsync() => _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}
