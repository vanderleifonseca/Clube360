using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Infrastructure.Contratos;

/// <summary>Tarefas agendadas dos contratos (Hangfire).</summary>
public sealed class JobsContratos(AppDbContext db, ContratoService contratos, ILogger<JobsContratos> log)
{
    public const string RenovarContratos = "renovar-contratos";

    /// <summary>Todo dia às 5h30: renova automaticamente os contratos com vigência vencida, em cada associação.</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task RenovarVencidosAsync()
    {
        var associacoes = await db.Associacoes.Where(a => a.Ativa).Select(a => new { a.Id, a.Nome }).ToListAsync();
        foreach (var a in associacoes)
        {
            db.Tenant.Definir(a.Id);
            db.ChangeTracker.Clear();
            var renovados = await contratos.RenovarVencidosAsync();
            if (renovados > 0) log.LogInformation("Associação {Associacao}: {Quantidade} contrato(s) renovado(s).", a.Nome, renovados);
        }
    }
}
