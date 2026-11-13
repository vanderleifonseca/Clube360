using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Socios;

namespace ClubeDaOnca.Api.Tests;

public static class SocioTeste
{
    /// <summary>
    /// Sócio novo nasce "Em adesão" (aguarda o contrato). Nos testes que não tratam de contrato,
    /// ativa direto, como a secretaria faz com sócios antigos.
    /// </summary>
    public static async Task AtivarAsync(HttpClient admin, Guid socioId)
    {
        var resp = await admin.PatchAsJsonAsync($"/socios/{socioId}/situacao", new AlterarSituacaoRequest("Ativo"));
        resp.EnsureSuccessStatusCode();
    }
}
