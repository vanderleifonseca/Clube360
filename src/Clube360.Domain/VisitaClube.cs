namespace Clube360.Domain;

public sealed record RegistroPortaria(DateTimeOffset Horario, string UsuarioId, string UsuarioNome);

public sealed record VisitaClube(Guid Id, Guid ClubeId, Guid AssociadoId, string Nome, Categoria Categoria,
    string FinalCpf, RegistroPortaria Entrada, RegistroPortaria? Saida = null);
