using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubeDaOnca.Api.Infrastructure.Pagamentos;

/// <summary>
/// Credenciais do Asaas da associação atual. Cada unidade pode ter a própria conta (chave guardada
/// criptografada no banco); sem chave própria, usa a conta geral configurada no servidor.
/// </summary>
public sealed class ContaAsaas(AppDbContext db, IDataProtectionProvider protecao, IOptions<PagamentosOptions> options)
{
    private IDataProtector Protetor => protecao.CreateProtector("ClubeDaOnca.Asaas.Associacao");

    public string Proteger(string valor) => Protetor.Protect(valor);

    public async Task<string?> ApiKeyAsync(CancellationToken ct = default)
    {
        if (db.Tenant.AssociacaoId is { } id)
        {
            var protegida = await db.Associacoes.AsNoTracking().Where(a => a.Id == id)
                .Select(a => a.AsaasApiKeyProtegida).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(protegida)) return Protetor.Unprotect(protegida);
        }
        return string.IsNullOrWhiteSpace(options.Value.Asaas.ApiKey) ? null : options.Value.Asaas.ApiKey;
    }

    /// <summary>Token esperado nos webhooks da associação (o dela, ou o geral do servidor).</summary>
    public async Task<string?> WebhookTokenAsync(Guid? associacaoId, CancellationToken ct = default)
    {
        if (associacaoId is { } id)
        {
            var protegido = await db.Associacoes.AsNoTracking().Where(a => a.Id == id)
                .Select(a => a.AsaasWebhookTokenProtegido).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(protegido)) return Protetor.Unprotect(protegido);
        }
        return string.IsNullOrWhiteSpace(options.Value.Asaas.WebhookToken) ? null : options.Value.Asaas.WebhookToken;
    }
}
