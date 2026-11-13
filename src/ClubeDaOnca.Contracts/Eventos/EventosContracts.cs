namespace ClubeDaOnca.Contracts.Eventos;

public sealed record LocalEventoDto(Guid Id, string Nome, string Tipo, int Capacidade);

/// <param name="VagasRestantes">Reservas ainda disponíveis neste ambiente.</param>
public sealed record AmbienteEventoDto(
    Guid Id,
    string Nome,
    int PessoasPorReserva,
    int Vagas,
    int VagasRestantes,
    int LimitePorSocio,
    decimal Valor);

/// <param name="Status">Rascunho, Publicado ou Cancelado.</param>
/// <param name="MinhasReservas">Reservas confirmadas do título do usuário logado (0 para a gestão).</param>
public sealed record EventoDto(
    Guid Id,
    string Titulo,
    string? Descricao,
    DateTime Inicio,
    DateTime Termino,
    string Status,
    int Capacidade,
    int PessoasReservadas,
    bool AbertoAoPublico,
    bool DivulgarAssociados,
    bool DivulgarRedesSociais,
    int PrazoCancelamentoHoras,
    string? MotivoCancelamento,
    IReadOnlyList<LocalEventoDto> Locais,
    IReadOnlyList<AmbienteEventoDto> Ambientes,
    int MinhasReservas);

/// <param name="Id">Nulo para ambiente novo; informado para manter um ambiente existente.</param>
public sealed record SalvarAmbienteRequest(Guid? Id, string Nome, int PessoasPorReserva, int Vagas, int LimitePorSocio, decimal Valor);

/// <param name="Inicio">Data e hora no fuso do clube.</param>
public sealed record SalvarEventoRequest(
    string Titulo,
    string? Descricao,
    DateTime Inicio,
    DateTime Termino,
    int Capacidade,
    bool AbertoAoPublico,
    bool DivulgarAssociados,
    bool DivulgarRedesSociais,
    int PrazoCancelamentoHoras,
    IReadOnlyList<Guid> Locais,
    IReadOnlyList<SalvarAmbienteRequest> Ambientes);

public sealed record VerificarAgendaRequest(IReadOnlyList<Guid> Locais, DateTime Inicio, DateTime Termino, Guid? IgnorarEventoId = null);

public sealed record ConflitoAgendaDto(string Local, DateTime Inicio, DateTime Fim, string Descricao);

/// <param name="Disponivel">Nenhuma reserva ou outro evento ocupa os locais no período.</param>
public sealed record DisponibilidadeDto(bool Disponivel, IReadOnlyList<ConflitoAgendaDto> Conflitos);

public sealed record CancelarEventoRequest(string Motivo);

public sealed record ReservarEventoRequest(Guid AmbienteId, int Pessoas, string? Observacao);

/// <param name="Status">Confirmada ou Cancelada.</param>
public sealed record ReservaEventoDto(
    Guid Id,
    Guid EventoId,
    string Evento,
    DateTime EventoInicio,
    Guid AmbienteId,
    string Ambiente,
    int Pessoas,
    decimal Valor,
    string Status,
    string? Observacao,
    bool PodeCancelar,
    string Socio,
    int NumeroTitulo,
    DateTimeOffset CriadoEm);
