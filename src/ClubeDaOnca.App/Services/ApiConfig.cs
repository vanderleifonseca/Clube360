using System.Reflection;

namespace ClubeDaOnca.App.Services;

public static class ApiConfig
{
    private const string ChaveServidor = "api_servidor";

    /// <summary>
    /// URL da API, nesta ordem:
    /// 1. o servidor digitado na tela de login (só no build de teste, para usar o celular pelo Wi-Fi);
    /// 2. a URL fixada no build com <c>-p:ApiUrl=http://192.168.x.x:5080</c>;
    /// 3. o padrão: 10.0.2.2 no emulador Android, localhost no Windows e HTTPS em produção.
    /// </summary>
    public static string BaseUrl => ServidorSalvo ?? UrlDoBuild ?? Padrao;

    /// <summary>O campo "Servidor" aparece na tela de login só no build de teste (Debug).</summary>
    public static bool PodeTrocarServidor =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>
    /// Salva o servidor digitado. Aceita "192.168.3.196", "192.168.3.196:5080" ou a URL completa;
    /// sem porta, usa a 5080 da API de desenvolvimento. Vazio volta ao padrão. Devolve a URL salva.
    /// </summary>
    public static string DefinirServidor(string? texto)
    {
        if (!PodeTrocarServidor) return BaseUrl;
        var valor = texto?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(valor))
        {
            Preferences.Default.Remove(ChaveServidor);
            return BaseUrl;
        }

        if (!valor.Contains("://")) valor = "http://" + valor;
        if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))
            throw new FormatException("Servidor inválido. Use, por exemplo, 192.168.3.196 ou http://192.168.3.196:5080.");

        var semPorta = uri.IsDefaultPort && !texto!.Contains($":{uri.Port}");
        var url = semPorta && uri.Scheme == "http" ? $"http://{uri.Host}:5080"
            : semPorta ? $"{uri.Scheme}://{uri.Host}"
            : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        Preferences.Default.Set(ChaveServidor, url);
        return url;
    }

    private static string? ServidorSalvo =>
        PodeTrocarServidor && Preferences.Default.Get<string?>(ChaveServidor, null) is { Length: > 0 } s ? s : null;

    private static readonly string? UrlDoBuild = typeof(ApiConfig).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "ApiUrl" && !string.IsNullOrWhiteSpace(a.Value))?.Value;

    private static string Padrao =>
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080" : "http://localhost:5080";
#else
        "https://api.clubedaonca.com.br";
#endif
}
