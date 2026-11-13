using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Domain.Comum;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth").WithTags("Autenticação");

        // O usuário informa só e-mail (ou CPF) e senha: a associação vem do cadastro da conta.
        g.MapPost("/login", async (LoginRequest req, UserManager<Usuario> users, TokenService tokens, AppDbContext db, CancellationToken ct) =>
        {
            var usuario = await BuscarPorLoginAsync(users, req.Login, ct);
            if (usuario is null || await users.IsLockedOutAsync(usuario))
                return Results.Unauthorized();

            if (!await users.CheckPasswordAsync(usuario, req.Senha))
            {
                await users.AccessFailedAsync(usuario);
                return Results.Unauthorized();
            }

            if (!await AssociacaoAtivaAsync(db, usuario, ct))
                return Results.Problem(title: "A associação desta conta está desativada.", statusCode: StatusCodes.Status403Forbidden);

            await users.ResetAccessFailedCountAsync(usuario);
            return Results.Ok(await tokens.EmitirAsync(usuario, ct));
        });

        g.MapPost("/refresh", async (RefreshRequest req, AppDbContext db, TokenService tokens, CancellationToken ct) =>
        {
            var hash = TokenService.Hash(req.RefreshToken);
            var atual = await db.RefreshTokens.Include(r => r.Usuario)
                .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
            if (atual is null || !atual.Ativo || atual.Usuario is null || !await AssociacaoAtivaAsync(db, atual.Usuario, ct))
                return Results.Unauthorized();

            // Rotação: o refresh token usado é revogado e um novo é emitido.
            atual.RevogadoEm = DateTimeOffset.UtcNow;
            return Results.Ok(await tokens.EmitirAsync(atual.Usuario, ct));
        });

        g.MapPost("/logout", async (RefreshRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var hash = TokenService.Hash(req.RefreshToken);
            await db.RefreshTokens.Where(r => r.TokenHash == hash && r.RevogadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevogadoEm, DateTimeOffset.UtcNow), ct);
            return Results.NoContent();
        });

        g.MapGet("/me", async (ClaimsPrincipal principal, UserManager<Usuario> users, TokenService tokens, CancellationToken ct) =>
        {
            var usuario = await users.GetUserAsync(principal);
            if (usuario is null) return Results.Unauthorized();
            var perfis = await users.GetRolesAsync(usuario);
            return Results.Ok(await tokens.MapearAsync(usuario, perfis, ct));
        }).RequireAuthorization();

        g.MapPost("/alterar-senha", async (AlterarSenhaRequest req, ClaimsPrincipal principal, UserManager<Usuario> users) =>
        {
            var usuario = await users.GetUserAsync(principal);
            if (usuario is null) return Results.Unauthorized();

            var resultado = await users.ChangePasswordAsync(usuario, req.SenhaAtual, req.NovaSenha);
            if (!resultado.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["senha"] = resultado.Errors.Select(e => TraduzirErroSenha(e.Code, e.Description)).ToArray(),
                });

            if (usuario.DeveTrocarSenha)
            {
                usuario.DeveTrocarSenha = false;
                await users.UpdateAsync(usuario);
            }
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    /// <summary>As mensagens padrão do Identity vêm em inglês.</summary>
    public static string TraduzirErroSenha(string codigo, string padrao) => codigo switch
    {
        "PasswordMismatch" => "Senha atual incorreta.",
        "PasswordTooShort" => "A senha deve ter pelo menos 8 caracteres.",
        "PasswordRequiresDigit" => "A senha deve ter pelo menos um número.",
        "PasswordRequiresLower" => "A senha deve ter pelo menos uma letra minúscula.",
        "PasswordRequiresUpper" => "A senha deve ter pelo menos uma letra maiúscula.",
        "PasswordRequiresUniqueChars" => "A senha deve ter mais caracteres diferentes.",
        _ => padrao,
    };

    /// <summary>Contas sem associação (só o SuperAdmin da rede) sempre entram.</summary>
    private static async Task<bool> AssociacaoAtivaAsync(AppDbContext db, Usuario usuario, CancellationToken ct) =>
        usuario.AssociacaoId is not { } id || await db.Associacoes.AnyAsync(a => a.Id == id && a.Ativa, ct);

    private static Task<Usuario?> BuscarPorLoginAsync(UserManager<Usuario> users, string login, CancellationToken ct)
    {
        login = login.Trim();
        if (login.Contains('@'))
            return users.FindByEmailAsync(login);

        var cpf = Cpf.Normalizar(login);
        return users.Users.FirstOrDefaultAsync(u => u.Cpf == cpf, ct);
    }
}
