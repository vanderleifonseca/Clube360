namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>
/// Associação (unidade) em que a requisição atual trabalha. Vem da claim <see cref="Claim"/> do JWT;
/// jobs, seed e webhooks definem explicitamente. Sem associação definida, as consultas não retornam nada.
/// </summary>
public sealed class TenantAtual(IHttpContextAccessor http)
{
    public const string Claim = "associacao_id";

    private Guid? _definido;

    public Guid? AssociacaoId
    {
        get
        {
            if (_definido is not null) return _definido;
            var valor = http.HttpContext?.User.FindFirst(Claim)?.Value;
            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    /// <summary>Desliga o filtro por associação (uso interno: seed, webhook do gateway, jobs que percorrem todas as unidades).</summary>
    public bool SemFiltro { get; private set; }

    public void Definir(Guid associacaoId) => _definido = associacaoId;

    public IDisposable DesligarFiltro()
    {
        var anterior = SemFiltro;
        SemFiltro = true;
        return new Restaurar(() => SemFiltro = anterior);
    }

    public Guid Exigir() => AssociacaoId ?? throw new InvalidOperationException("Nenhuma associação definida para a operação.");

    private sealed class Restaurar(Action acao) : IDisposable
    {
        public void Dispose() => acao();
    }
}
