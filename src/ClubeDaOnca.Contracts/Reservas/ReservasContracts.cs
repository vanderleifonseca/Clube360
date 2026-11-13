namespace ClubeDaOnca.Contracts.Reservas;

/// <param name="DiasSemana">0 = domingo ... 6 = sábado.</param>
public sealed record EspacoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool Ativo,
    TimeOnly Abertura,
    TimeOnly Fechamento,
    int DuracaoMinutos,
    IReadOnlyList<int> DiasSemana,
    int AntecedenciaMinimaHoras,
    int AntecedenciaMaximaDias,
    int LimiteReservasAtivas,
    decimal Taxa,
    bool ExigeAprovacao,
    int PrazoCancelamentoHoras,
    string Tipo = "Outro",
    int Capacidade = 0);

/// <param name="Tipo">Quadra, Quiosque, Churrasqueira, Salao, Piscina, Campo, Auditorio ou Outro.</param>
/// <param name="Capacidade">Pessoas que o local comporta (0 = não informada).</param>
public sealed record SalvarEspacoRequest(
    string Nome,
    string? Descricao,
    bool Ativo,
    TimeOnly Abertura,
    TimeOnly Fechamento,
    int DuracaoMinutos,
    IReadOnlyList<int> DiasSemana,
    int AntecedenciaMinimaHoras,
    int AntecedenciaMaximaDias,
    int LimiteReservasAtivas,
    decimal Taxa,
    bool ExigeAprovacao,
    int PrazoCancelamentoHoras,
    string? Tipo = null,
    int Capacidade = 0);

/// <param name="Situacao">"Livre", "Ocupado" ou "Indisponivel".</param>
public sealed record HorarioDto(TimeOnly Inicio, TimeOnly Fim, string Situacao);

public sealed record AgendaDto(Guid EspacoId, DateOnly Data, IReadOnlyList<HorarioDto> Horarios);

public sealed record CriarReservaRequest(Guid EspacoId, DateOnly Data, TimeOnly Inicio, string? Observacao);

/// <param name="Status">"Pendente", "Confirmada", "Cancelada" ou "Recusada".</param>
/// <param name="PodeCancelar">Se o sócio ainda pode cancelar pelo app.</param>
public sealed record ReservaDto(
    Guid Id,
    Guid EspacoId,
    string Espaco,
    DateOnly Data,
    TimeOnly Inicio,
    TimeOnly Fim,
    string Status,
    decimal Taxa,
    string? Observacao,
    string? MotivoRecusa,
    bool PodeCancelar,
    int NumeroTitulo,
    string Socio,
    DateTimeOffset CriadoEm);

public sealed record RecusarReservaRequest(string Motivo);
