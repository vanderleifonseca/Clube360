namespace ClubeDaOnca.Contracts.Auth;

/// <param name="Login">CPF (com ou sem máscara) ou e-mail.</param>
public sealed record LoginRequest(string Login, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiraEm,
    string RefreshToken,
    UsuarioDto Usuario);

/// <param name="DeveTrocarSenha">O app obriga a troca da senha provisória antes de seguir.</param>
/// <param name="AssociacaoId">Associação (unidade) em que o usuário está cadastrado, descoberta pelo login.</param>
/// <param name="Associacao">Nome da associação, para exibir no app.</param>
public sealed record UsuarioDto(
    string Id,
    string Nome,
    string Email,
    IReadOnlyList<string> Perfis,
    bool DeveTrocarSenha = false,
    Guid? AssociacaoId = null,
    string? Associacao = null);
