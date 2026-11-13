using System.Net;
using System.Net.Http.Headers;

namespace ClubeDaOnca.App.Services;

/// <summary>Adiciona o Bearer token e, em caso de 401, renova o token e repete a requisição uma vez.</summary>
public sealed class AuthHeaderHandler(SessaoService sessao) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await sessao.ObterAccessTokenAsync(ct: ct);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || token is null)
            return response;

        token = await sessao.ObterAccessTokenAsync(forcarRenovacao: true, ct);
        if (token is null)
            return response;

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
