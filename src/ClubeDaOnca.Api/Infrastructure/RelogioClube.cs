namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>Hora local do clube (reservas usam data e hora no fuso do clube, não UTC).</summary>
public sealed class RelogioClube(TimeProvider tempo, IConfiguration config)
{
    private readonly TimeZoneInfo _fuso = TimeZoneInfo.FindSystemTimeZoneById(config["Clube:FusoHorario"] ?? "America/Sao_Paulo");

    public DateTimeOffset UtcAgora => tempo.GetUtcNow();
    public DateTime Agora => TimeZoneInfo.ConvertTime(tempo.GetUtcNow(), _fuso).DateTime;
    public DateOnly Hoje => DateOnly.FromDateTime(Agora);

    /// <summary>Converte um instante (UTC) para a hora local do clube.</summary>
    public DateTime ParaLocal(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, _fuso).DateTime;
}
