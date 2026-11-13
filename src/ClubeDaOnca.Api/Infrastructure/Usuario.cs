using Microsoft.AspNetCore.Identity;

namespace ClubeDaOnca.Api.Infrastructure;

public sealed class Usuario : IdentityUser
{
    public string Nome { get; set; } = string.Empty;

    /// <summary>CPF somente com dígitos.</summary>
    public string? Cpf { get; set; }

    /// <summary>Verdadeiro enquanto o usuário ainda usa a senha provisória entregue pela secretaria.</summary>
    public bool DeveTrocarSenha { get; set; }

    /// <summary>Associação (unidade) em que a conta foi cadastrada; o login leva o usuário direto para ela.</summary>
    public Guid? AssociacaoId { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UsuarioId { get; set; } = string.Empty;
    public Usuario? Usuario { get; set; }

    /// <summary>SHA-256 do token; o valor em texto nunca é guardado.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiraEm { get; set; }
    public DateTimeOffset? RevogadoEm { get; set; }

    public bool Ativo => RevogadoEm is null && ExpiraEm > DateTimeOffset.UtcNow;
}
