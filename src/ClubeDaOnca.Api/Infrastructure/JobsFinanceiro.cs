using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>Tarefas agendadas do financeiro (executadas pelo Hangfire).</summary>
public sealed class JobsFinanceiro(AppDbContext db, FinanceiroService fin, RelogioClube relogio, ILogger<JobsFinanceiro> log)
{
    public const string GerarMensalidades = "gerar-mensalidades";

    /// <summary>Todo dia 1º às 6h: gera as mensalidades do mês em cada associação ativa. Repetir não duplica.</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task GerarMensalidadesDoMesAsync()
    {
        var associacoes = await db.Associacoes.Where(a => a.Ativa).Select(a => new { a.Id, a.Nome }).ToListAsync();
        foreach (var a in associacoes)
        {
            db.Tenant.Definir(a.Id);
            db.ChangeTracker.Clear();
            log.LogInformation("Gerando mensalidades da associação {Associacao}.", a.Nome);
            await fin.GerarMensalidadesAsync(relogio.Hoje);
        }
    }

    /// <summary>Emite os boletos das parcelas do contrato; se algum falhar, o Hangfire tenta de novo (só o que faltou).</summary>
    [AutomaticRetry(Attempts = 5)]
    public async Task EmitirBoletosContratoAsync(Guid associacaoId, Guid contratoId)
    {
        db.Tenant.Definir(associacaoId);
        var (_, falhas) = await fin.EmitirBoletosContratoAsync(contratoId);
        if (falhas > 0)
            throw new Pagamentos.GatewayException($"{falhas} boleto(s) do contrato não foram emitidos; nova tentativa agendada.");
    }
}
