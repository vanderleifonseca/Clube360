using Hangfire;

namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>
/// Dispara a emissão dos 12 boletos do contrato. Com o Hangfire ligado, vai para a fila (com novas tentativas
/// se o gateway falhar) e a tela do presidente não espera o Asaas; sem Hangfire (testes), emite na hora.
/// </summary>
public sealed class EmissaoBoletos(IServiceProvider services, FinanceiroService financeiro, AppDbContext db, ILogger<EmissaoBoletos> log)
{
    public async Task SolicitarAsync(Guid contratoId, CancellationToken ct = default)
    {
        var associacaoId = db.Tenant.Exigir();
        if (services.GetService<IBackgroundJobClient>() is { } fila)
        {
            fila.Enqueue<JobsFinanceiro>(j => j.EmitirBoletosContratoAsync(associacaoId, contratoId));
            return;
        }

        var (_, falhas) = await financeiro.EmitirBoletosContratoAsync(contratoId, ct);
        if (falhas > 0)
            log.LogWarning("Contrato {Contrato}: {Falhas} boleto(s) não emitido(s); use \"Emitir boletos pendentes\".", contratoId, falhas);
    }
}
