namespace ClubeDaOnca.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    public string Issuer { get; set; } = "ClubeDaOnca";
    public string Audience { get; set; } = "ClubeDaOnca.App";

    /// <summary>Chave HMAC com no mínimo 32 caracteres. Em produção, vem de variável de ambiente (Jwt__Chave).</summary>
    public string Chave { get; set; } = string.Empty;
    public int AccessTokenMinutos { get; set; } = 15;
    public int RefreshTokenDias { get; set; } = 30;
}
