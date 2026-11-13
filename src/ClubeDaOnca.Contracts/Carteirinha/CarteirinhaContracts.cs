namespace ClubeDaOnca.Contracts.Carteirinha;

/// <param name="Tipo">"Titular" ou "Dependente".</param>
/// <param name="ChaveQr">
/// Chave (Base64) com que o app gera o QR dinâmico, inclusive offline.
/// Deve ser guardada apenas no SecureStorage.
/// </param>
public sealed record CarteirinhaDto(
    Guid PessoaId,
    string Nome,
    string Tipo,
    int NumeroTitulo,
    string Categoria,
    string Situacao,
    string? Parentesco,
    DateOnly DataNascimento,
    string ChaveQr);

public sealed record ValidarQrRequest(string Token);

/// <param name="Tipo">"Titular", "Dependente" ou nulo quando o QR não foi reconhecido.</param>
public sealed record ResultadoAcessoDto(
    bool Liberado,
    string Motivo,
    string? Nome,
    string? Tipo,
    int? NumeroTitulo,
    string? Situacao,
    DateTimeOffset Em,
    bool Inadimplente = false);

public sealed record RegistroAcessoDto(
    Guid Id,
    string? Nome,
    bool Liberado,
    string Motivo,
    DateTimeOffset Em);
