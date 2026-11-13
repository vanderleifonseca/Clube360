using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubeDaOnca.Contracts.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClubeDaOnca.Api.Infrastructure;

public sealed class TokenService(
    IOptions<JwtOptions> options,
    UserManager<Usuario> userManager,
    AppDbContext db)
{
    private readonly JwtOptions _opt = options.Value;

    public async Task<AuthResponse> EmitirAsync(Usuario usuario, CancellationToken ct = default)
    {
        var perfis = (await userManager.GetRolesAsync(usuario)).ToList();
        var expira = DateTimeOffset.UtcNow.AddMinutes(_opt.AccessTokenMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(perfis.Select(p => new Claim(ClaimTypes.Role, p)));
        if (usuario.AssociacaoId is { } associacaoId)
            claims.Add(new Claim(TenantAtual.Claim, associacaoId.ToString()));

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Chave)), SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims,
            expires: expira.UtcDateTime, signingCredentials: credenciais);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = Hash(refresh),
            ExpiraEm = DateTimeOffset.UtcNow.AddDays(_opt.RefreshTokenDias),
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            expira,
            refresh,
            await MapearAsync(usuario, perfis, ct));
    }

    public async Task<UsuarioDto> MapearAsync(Usuario usuario, IList<string> perfis, CancellationToken ct = default)
    {
        string? associacao = null;
        if (usuario.AssociacaoId is { } id)
            associacao = await db.Associacoes.Where(a => a.Id == id).Select(a => a.Nome).FirstOrDefaultAsync(ct);
        return new UsuarioDto(usuario.Id, usuario.Nome, usuario.Email ?? string.Empty, perfis.ToList(),
            usuario.DeveTrocarSenha, usuario.AssociacaoId, associacao);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
