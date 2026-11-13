using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClubeDaOnca.Domain.Financeiro;

namespace ClubeDaOnca.Api.Infrastructure.Pagamentos;

/// <summary>Integração com a API v3 do Asaas (clientes, cobranças Pix e boleto).</summary>
public sealed class AsaasGateway(HttpClient http, ContaAsaas conta, ILogger<AsaasGateway> log) : IGatewayPagamento
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Nome => "Asaas";
    public bool Simulado => false;

    public static void Configurar(HttpClient http, AsaasOptions opt)
    {
        // A chave vai em cada requisição: cada associação pode ter a própria conta.
        http.BaseAddress = new Uri(opt.Url.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClubeDaOnca/1.0");
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<CobrancaGateway> CriarAsync(DadosPagador pagador, Cobranca cobranca, FormaPagamento forma,
        DateOnly vencimento, CancellationToken ct)
    {
        var cliente = await ObterOuCriarClienteAsync(pagador, ct);

        var pagamento = await EnviarAsync<AsaasPagamento>(HttpMethod.Post, "payments", new
        {
            customer = cliente,
            billingType = forma == FormaPagamento.Pix ? "PIX" : "BOLETO",
            value = cobranca.Valor,
            dueDate = vencimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            description = cobranca.Descricao,
            externalReference = cobranca.Id.ToString(),
        }, ct);

        if (forma == FormaPagamento.Pix)
        {
            var pix = await EnviarAsync<AsaasPix>(HttpMethod.Get, $"payments/{pagamento.Id}/pixQrCode", null, ct);
            return new CobrancaGateway(pagamento.Id, pix.Payload, pix.EncodedImage,
                DateTimeOffset.TryParse(pix.ExpirationDate, CultureInfo.InvariantCulture, out var exp) ? exp : null,
                null, null);
        }

        var linha = await EnviarAsync<AsaasLinhaDigitavel>(HttpMethod.Get, $"payments/{pagamento.Id}/identificationField", null, ct);
        return new CobrancaGateway(pagamento.Id, null, null, null, pagamento.BankSlipUrl, linha.IdentificationField);
    }

    public async Task CancelarAsync(string idExterno, CancellationToken ct) =>
        await EnviarAsync<object>(HttpMethod.Delete, $"payments/{idExterno}", null, ct);

    private async Task<string> ObterOuCriarClienteAsync(DadosPagador p, CancellationToken ct)
    {
        var existentes = await EnviarAsync<AsaasLista<AsaasCliente>>(HttpMethod.Get, $"customers?cpfCnpj={p.Cpf}", null, ct);
        if (existentes.Data.FirstOrDefault() is { } c)
            return c.Id;

        var novo = await EnviarAsync<AsaasCliente>(HttpMethod.Post, "customers", new
        {
            name = p.Nome,
            cpfCnpj = p.Cpf,
            email = p.Email,
            mobilePhone = p.Telefone is null ? null : new string(p.Telefone.Where(char.IsDigit).ToArray()),
            externalReference = p.SocioId.ToString(),
            postalCode = p.Cep,
            address = p.Logradouro,
            addressNumber = p.Numero,
            complement = p.Complemento,
            province = p.Bairro,
            notificationDisabled = true, // os avisos saem pelo app do clube
        }, ct);
        return novo.Id;
    }

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
    {
        var chave = await conta.ApiKeyAsync(ct)
            ?? throw new GatewayException("Conta do Asaas não configurada: cadastre a chave da API da associação ou a conta geral do servidor.");

        using var req = new HttpRequestMessage(metodo, caminho);
        req.Headers.Add("access_token", chave);
        if (corpo is not null) req.Content = JsonContent.Create(corpo, options: Json);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new GatewayException("Não foi possível falar com o Asaas. Tente novamente em instantes.", ex);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var erro = await resp.Content.ReadAsStringAsync(ct);
                log.LogWarning("Asaas {Metodo} {Caminho} respondeu {Status}: {Erro}", metodo, caminho, (int)resp.StatusCode, erro);
                throw new GatewayException(MensagemDeErro(erro) ?? $"O Asaas recusou a operação ({(int)resp.StatusCode}).");
            }
            return (await resp.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }

    private static string? MensagemDeErro(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0
                ? e[0].GetProperty("description").GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record AsaasLista<T>(List<T> Data);
    private sealed record AsaasCliente(string Id);
    private sealed record AsaasPagamento(string Id, string? BankSlipUrl);
    private sealed record AsaasPix(string EncodedImage, string Payload, string? ExpirationDate);
    private sealed record AsaasLinhaDigitavel(string IdentificationField);
}
