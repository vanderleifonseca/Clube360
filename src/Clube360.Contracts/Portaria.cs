namespace Clube360.Contracts;

public sealed record EntradaPortariaRequest(Guid AssociadoId);
public sealed record PessoaPortariaDto(Guid Id, string Nome, string Categoria, string FinalCpf);
public sealed record VisitaPortariaDto(Guid Id, Guid AssociadoId, string Nome, string Categoria, string FinalCpf,
    DateTimeOffset EntradaEm, string EntradaPor, DateTimeOffset? SaidaEm, string? SaidaPor);
public sealed record PortariaDto(DateOnly Dia, IReadOnlyList<PessoaPortariaDto> Elegiveis,
    IReadOnlyList<VisitaPortariaDto> Presentes, IReadOnlyList<VisitaPortariaDto> Historico);
