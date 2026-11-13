using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Reservas;
using ClubeDaOnca.Domain.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Infrastructure;

public static class Seed
{
    public static async Task ExecutarAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // O seed trabalha na associação padrão (criada pela migração MultiAssociacao).
        db.Tenant.Definir(Associacao.PadraoId);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var perfil in Perfis.Todos)
            if (!await roles.RoleExistsAsync(perfil))
                await roles.CreateAsync(new IdentityRole(perfil));

        // Espaços de exemplo (só quando configurado, ex.: em desenvolvimento).
        if (config.GetValue<bool>("Seed:EspacosExemplo") && !await db.Espacos.AnyAsync())
        {
            db.Espacos.AddRange(
                new Espaco
                {
                    Nome = "Quadra de tênis", Descricao = "Quadra de saibro com iluminação.",
                    Abertura = new TimeOnly(7, 0), Fechamento = new TimeOnly(22, 0), DuracaoMinutos = 60,
                    AntecedenciaMinimaHoras = 1, AntecedenciaMaximaDias = 7, LimiteReservasAtivas = 2, PrazoCancelamentoHoras = 2,
                },
                new Espaco
                {
                    Nome = "Churrasqueira 1", Descricao = "Até 30 pessoas, com pia e geladeira.",
                    Abertura = new TimeOnly(10, 0), Fechamento = new TimeOnly(22, 0), DuracaoMinutos = 720,
                    AntecedenciaMinimaHoras = 24, AntecedenciaMaximaDias = 60, LimiteReservasAtivas = 1,
                    Taxa = 80m, PrazoCancelamentoHoras = 48,
                },
                new Espaco
                {
                    Nome = "Salão de festas", Descricao = "Até 120 pessoas. Sujeito a aprovação da secretaria.",
                    Abertura = new TimeOnly(18, 0), Fechamento = new TimeOnly(0, 0), DuracaoMinutos = 360,
                    DiasSemana = [5, 6], AntecedenciaMinimaHoras = 72, AntecedenciaMaximaDias = 180,
                    LimiteReservasAtivas = 1, Taxa = 450m, ExigeAprovacao = true, PrazoCancelamentoHoras = 168,
                });
            await db.SaveChangesAsync();
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        // Admin inicial: só é criado se as credenciais estiverem configuradas.
        var email = config["AdminInicial:Email"];
        var senha = config["AdminInicial:Senha"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(senha))
            await CriarAdminInicialAsync(users, email, senha);

        await CriarUsuariosExemploAsync(db, users, config);
    }

    /// <summary>Contas de exemplo (portaria, presidente...) definidas em Seed:UsuariosExemplo, só para desenvolvimento.</summary>
    private static async Task CriarUsuariosExemploAsync(AppDbContext db, UserManager<Usuario> users, IConfiguration config)
    {
        foreach (var item in config.GetSection("Seed:UsuariosExemplo").GetChildren())
        {
            var email = item["Email"];
            var senha = item["Senha"];
            var perfil = item["Perfil"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(perfil))
                continue;
            var nome = item["Nome"] ?? email;

            if (await users.FindByEmailAsync(email) is null)
            {
                var usuario = new Usuario
                {
                    UserName = email, Email = email, EmailConfirmed = true, Nome = nome,
                    AssociacaoId = Associacao.PadraoId,
                };
                var result = await users.CreateAsync(usuario, senha);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Falha ao criar usuário de exemplo {email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
                await users.AddToRoleAsync(usuario, perfil);
            }

            if (!item.GetValue<bool>("Presidente")) continue;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var temVigente = await db.Mandatos.AnyAsync(m =>
                m.AssociacaoId == Associacao.PadraoId && m.Inicio <= hoje && m.Fim >= hoje);
            if (!temVigente)
            {
                db.Mandatos.Add(new MandatoPresidente
                {
                    AssociacaoId = Associacao.PadraoId, Nome = nome, Email = email,
                    Inicio = hoje, Fim = RegrasAssociacao.FimDoMandato(hoje),
                });
                await db.SaveChangesAsync();
            }
        }
    }

    // O admin inicial administra a associação padrão e também a rede (cadastra novas associações).
    private static async Task CriarAdminInicialAsync(UserManager<Usuario> users, string email, string senha)
    {
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new Usuario
            {
                UserName = email, Email = email, EmailConfirmed = true, Nome = "Administrador",
                AssociacaoId = Associacao.PadraoId,
            };
            var result = await users.CreateAsync(admin, senha);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Falha ao criar admin inicial: " + string.Join("; ", result.Errors.Select(e => e.Description)));
            await users.AddToRolesAsync(admin, [Perfis.Admin, Perfis.Titular, Perfis.SuperAdmin]);
        }
        else if (!await users.IsInRoleAsync(admin, Perfis.SuperAdmin))
        {
            await users.AddToRoleAsync(admin, Perfis.SuperAdmin);
        }
    }
}
