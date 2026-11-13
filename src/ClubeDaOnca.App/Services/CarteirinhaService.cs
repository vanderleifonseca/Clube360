using System.Text.Json;
using ClubeDaOnca.Contracts.Carteirinha;

namespace ClubeDaOnca.App.Services;

/// <summary>
/// Busca as carteirinhas na API e guarda uma cópia no SecureStorage
/// (inclui as chaves do QR), para a carteirinha funcionar sem internet.
/// </summary>
public sealed class CarteirinhaService(IClubeApi api)
{
    private const string Chave = "carteirinhas";

    public async Task<(List<CarteirinhaDto> Itens, bool Offline)> ObterAsync(CancellationToken ct = default)
    {
        try
        {
            var itens = await api.CarteirinhasAsync(ct);
            await SecureStorage.Default.SetAsync(Chave, JsonSerializer.Serialize(itens));
            return (itens, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (await LerCacheAsync(), true);
        }
    }

    public static void LimparCache() => SecureStorage.Default.Remove(Chave);

    private static async Task<List<CarteirinhaDto>> LerCacheAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(Chave);
            return json is null ? [] : JsonSerializer.Deserialize<List<CarteirinhaDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
