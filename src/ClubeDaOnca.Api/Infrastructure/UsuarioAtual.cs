namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>Quem está fazendo a requisição (para o histórico de alterações). Sem usuário: "Sistema".</summary>
public sealed class UsuarioAtual(IHttpContextAccessor http)
{
    public string? Id => http.HttpContext?.User.FindFirst("sub")?.Value;
    public string Nome => http.HttpContext?.User.FindFirst("name")?.Value is { Length: > 0 } nome ? nome : "Sistema";
}
