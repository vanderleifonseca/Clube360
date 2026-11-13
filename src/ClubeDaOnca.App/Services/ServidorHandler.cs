namespace ClubeDaOnca.App.Services;

/// <summary>
/// Manda cada requisição para o servidor atual de <see cref="ApiConfig.BaseUrl"/>. Assim, trocar o servidor na
/// tela de login vale na hora, sem reiniciar o app. Os clientes HTTP guardam o endereço da criação.
/// </summary>
public sealed class ServidorHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } atual)
        {
            var servidor = new Uri(ApiConfig.BaseUrl);
            if (servidor.Scheme != atual.Scheme || servidor.Host != atual.Host || servidor.Port != atual.Port)
                request.RequestUri = new UriBuilder(atual) { Scheme = servidor.Scheme, Host = servidor.Host, Port = servidor.Port }.Uri;
        }
        return base.SendAsync(request, ct);
    }
}
