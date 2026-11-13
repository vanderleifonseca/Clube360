using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClubeDaOnca.Contracts.Associacoes;

namespace ClubeDaOnca.App.Services;

/// <summary>Busca o endereço pelo CEP no ViaCEP (serviço público dos Correios). Sem internet ou CEP inexistente: nulo.</summary>
public static class ViaCepService
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://viacep.com.br/"), Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<EnderecoDto?> BuscarAsync(string? cep, CancellationToken ct = default)
    {
        var digitos = new string((cep ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length != 8) return null;
        try
        {
            var r = await Http.GetFromJsonAsync<RespostaViaCep>($"ws/{digitos}/json/", ct);
            if (r is null || r.Erro) return null;
            return new EnderecoDto(digitos, r.Logradouro, null, r.Complemento, r.Bairro, r.Localidade, r.Uf);
        }
        catch
        {
            return null;
        }
    }

    private sealed record RespostaViaCep(
        [property: JsonPropertyName("logradouro")] string? Logradouro,
        [property: JsonPropertyName("complemento")] string? Complemento,
        [property: JsonPropertyName("bairro")] string? Bairro,
        [property: JsonPropertyName("localidade")] string? Localidade,
        [property: JsonPropertyName("uf")] string? Uf,
        [property: JsonPropertyName("erro")] bool Erro = false);
}
