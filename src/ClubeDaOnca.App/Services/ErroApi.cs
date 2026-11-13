using System.Net;
using System.Text.Json;
using Refit;

namespace ClubeDaOnca.App.Services;

/// <summary>Converte erros da API (ProblemDetails) em mensagens para o usuário.</summary>
public static class ErroApi
{
    public static string Mensagem(Exception ex) => ex switch
    {
        ApiException api when api.StatusCode == HttpStatusCode.Forbidden => "Você não tem permissão para esta ação.",
        ApiException api => LerProblema(api.Content) ?? $"Erro no servidor ({(int)api.StatusCode}).",
        HttpRequestException or TaskCanceledException => "Não foi possível conectar ao servidor. Verifique sua internet.",
        _ => "Ocorreu um erro inesperado.",
    };

    private static string? LerProblema(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            // Erros de validação: junta todas as mensagens.
            if (raiz.TryGetProperty("errors", out var erros) && erros.ValueKind == JsonValueKind.Object)
            {
                var mensagens = erros.EnumerateObject()
                    .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array
                        ? p.Value.EnumerateArray().Select(e => e.GetString())
                        : [])
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();
                if (mensagens.Count > 0) return string.Join("\n", mensagens);
            }

            return raiz.TryGetProperty("title", out var titulo) ? titulo.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
