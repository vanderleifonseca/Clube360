using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ClubeDaOnca.Api.Infrastructure.Comunicacao;

public sealed class WhatsAppOptions
{
    public const string Secao = "Comunicacao:WhatsApp";

    /// <summary>"Meta" (API oficial do WhatsApp Business) ou "Memoria" (testes).</summary>
    public string Modo { get; set; } = "Meta";
    public string Url { get; set; } = "https://graph.facebook.com/v21.0";

    /// <summary>Token de acesso permanente do usuário do sistema (Meta Business).</summary>
    public string? Token { get; set; }

    /// <summary>ID do número de telefone no WhatsApp Business (não é o número em si).</summary>
    public string? PhoneNumberId { get; set; }

    /// <summary>
    /// Nome do modelo de mensagem aprovado pela Meta, com 3 variáveis no corpo:
    /// {{1}} nome do sócio, {{2}} título do comunicado, {{3}} mensagem.
    /// </summary>
    public string Modelo { get; set; } = "comunicado_clube";
    public string Idioma { get; set; } = "pt_BR";
}

public interface IEnvioWhatsApp
{
    bool Configurado { get; }
    string? Detalhe { get; }

    /// <param name="telefone">Número no formato internacional, só dígitos (ex.: 5545999990000).</param>
    Task EnviarAsync(string telefone, string nome, string titulo, string mensagem, CancellationToken ct = default);
}

/// <summary>WhatsApp Business Cloud API (Meta). Fora da janela de 24 h só é permitido enviar modelos aprovados.</summary>
public sealed class WhatsAppMeta(HttpClient http, IOptions<WhatsAppOptions> options, ILogger<WhatsAppMeta> log) : IEnvioWhatsApp
{
    private WhatsAppOptions Opt => options.Value;

    public bool Configurado => !string.IsNullOrWhiteSpace(Opt.Token) && !string.IsNullOrWhiteSpace(Opt.PhoneNumberId);

    public string? Detalhe => Configurado
        ? $"API oficial da Meta · modelo \"{Opt.Modelo}\""
        : "Configure Comunicacao:WhatsApp:Token e Comunicacao:WhatsApp:PhoneNumberId (Meta Business).";

    public async Task EnviarAsync(string telefone, string nome, string titulo, string mensagem, CancellationToken ct = default)
    {
        if (!Configurado) throw new InvalidOperationException("WhatsApp não configurado.");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Opt.Url.TrimEnd('/')}/{Opt.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = telefone,
                type = "template",
                template = new
                {
                    name = Opt.Modelo,
                    language = new { code = Opt.Idioma },
                    components = new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = Domain.Comunicacao.RegrasComunicacao.Resumir(nome, 60) },
                                new { type = "text", text = Domain.Comunicacao.RegrasComunicacao.Resumir(titulo, 120) },
                                new { type = "text", text = Domain.Comunicacao.RegrasComunicacao.Resumir(mensagem, 800) },
                            },
                        },
                    },
                },
            }),
        };
        req.Headers.Authorization = new("Bearer", Opt.Token);

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            log.LogWarning("WhatsApp recusou o envio para {Telefone}: {Status} {Corpo}", telefone, (int)resp.StatusCode, corpo);
            throw new InvalidOperationException(MensagemDeErro(corpo) ?? $"WhatsApp recusou o envio ({(int)resp.StatusCode}).");
        }
    }

    private static string? MensagemDeErro(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Testes: guarda as mensagens.</summary>
public sealed class WhatsAppMemoria : IEnvioWhatsApp
{
    public ConcurrentQueue<(string Telefone, string Titulo, string Mensagem)> Enviados { get; } = new();
    public bool Configurado => true;
    public string? Detalhe => "Memória (testes)";

    public Task EnviarAsync(string telefone, string nome, string titulo, string mensagem, CancellationToken ct = default)
    {
        Enviados.Enqueue((telefone, titulo, mensagem));
        return Task.CompletedTask;
    }
}
