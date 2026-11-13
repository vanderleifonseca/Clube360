using ClubeDaOnca.Contracts.Associacoes;

namespace ClubeDaOnca.Contracts.Socios;

// Enums trafegam como texto (ex.: "Ativo") para ficarem legíveis no app e na documentação.

public sealed record SocioResumoDto(
    Guid Id,
    int NumeroTitulo,
    string Nome,
    string Cpf,
    string Categoria,
    string Situacao,
    int QuantidadeDependentes,
    string Genero = "NaoInformado",
    string? StatusContrato = null);

/// <param name="Tipo">Sempre "Dependente" (o titular é o <see cref="SocioDto"/>).</param>
public sealed record DependenteDto(
    Guid Id,
    string Nome,
    string? Cpf,
    DateOnly DataNascimento,
    string Parentesco,
    bool Ativo,
    string Genero = "NaoInformado",
    string? Email = null,
    string? Telefone = null,
    string? Profissao = null,
    string Tipo = "Dependente",
    string? TitularNome = null);

public sealed record SocioDto(
    Guid Id,
    int NumeroTitulo,
    string Nome,
    string Cpf,
    string? Email,
    string? Telefone,
    DateOnly DataNascimento,
    string Categoria,
    string Situacao,
    bool PossuiAcessoAoApp,
    IReadOnlyList<DependenteDto> Dependentes,
    string Genero = "NaoInformado",
    string? Profissao = null,
    EnderecoDto? Endereco = null,
    DateOnly? DataCadastro = null,
    DateTimeOffset? CriadoEm = null,
    DateTimeOffset? UltimaAlteracaoEm = null,
    string? UltimaAlteracaoPor = null,
    string Tipo = "Titular",
    string? Nacionalidade = null,
    string EstadoCivil = "NaoInformado",
    string? Rg = null,
    string? StatusContrato = null);

public sealed record SalvarSocioRequest(
    string Nome,
    string Cpf,
    string? Email,
    string? Telefone,
    DateOnly DataNascimento,
    string Categoria,
    string? Genero = null,
    string? Profissao = null,
    EnderecoDto? Endereco = null,
    DateOnly? DataCadastro = null,
    string? Nacionalidade = null,
    string? EstadoCivil = null,
    string? Rg = null);

/// <param name="SenhaProvisoria">
/// Senha de primeiro acesso ao app, exibida uma única vez para a secretaria entregar ao sócio.
/// Nula quando o sócio já tinha conta.
/// </param>
public sealed record SocioCriadoResponse(SocioDto Socio, string Login, string? SenhaProvisoria);

public sealed record AlterarSituacaoRequest(string Situacao);

public sealed record SalvarDependenteRequest(
    string Nome,
    string? Cpf,
    DateOnly DataNascimento,
    string Parentesco,
    string? Genero = null,
    string? Email = null,
    string? Telefone = null,
    string? Profissao = null);

/// <summary>Uma alteração do cadastro (do titular ou de um dependente).</summary>
/// <param name="Descricao">Quem foi alterado, quando não é o próprio titular (ex.: "Dependente Ana Souza").</param>
public sealed record HistoricoDto(
    DateTimeOffset Em,
    string UsuarioNome,
    string Acao,
    string? Descricao,
    string? Campo,
    string? ValorAnterior,
    string? ValorNovo);

public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int TamanhoPagina);
