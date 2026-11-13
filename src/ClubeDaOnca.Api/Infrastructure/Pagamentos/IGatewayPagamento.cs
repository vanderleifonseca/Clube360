using ClubeDaOnca.Domain.Financeiro;

namespace ClubeDaOnca.Api.Infrastructure.Pagamentos;

public sealed record DadosPagador(
    Guid SocioId,
    string Nome,
    string Cpf,
    string? Email,
    string? Telefone,
    string? Cep = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? Bairro = null)
{
    public static DadosPagador De(Domain.Socios.Socio s) => new(s.Id, s.Nome, s.Cpf, s.Email, s.Telefone,
        s.Endereco.Cep, s.Endereco.Logradouro, s.Endereco.Numero, s.Endereco.Complemento, s.Endereco.Bairro);
}

public sealed record CobrancaGateway(
    string IdExterno,
    string? PixCopiaECola,
    string? PixImagemBase64,
    DateTimeOffset? PixExpiraEm,
    string? BoletoUrl,
    string? LinhaDigitavel);

/// <summary>Gateway de pagamento (Asaas em produção; simulado em desenvolvimento).</summary>
public interface IGatewayPagamento
{
    string Nome { get; }
    bool Simulado { get; }

    Task<CobrancaGateway> CriarAsync(DadosPagador pagador, Cobranca cobranca, FormaPagamento forma,
        DateOnly vencimento, CancellationToken ct);

    /// <summary>Cancela a cobrança no gateway (boleto/Pix deixa de valer).</summary>
    Task CancelarAsync(string idExterno, CancellationToken ct);
}

public sealed class PagamentosOptions
{
    public const string Secao = "Pagamentos";

    /// <summary>"Simulado" ou "Asaas".</summary>
    public string Gateway { get; set; } = string.Empty;

    public AsaasOptions Asaas { get; set; } = new();
}

public sealed class AsaasOptions
{
    /// <summary>Sandbox: https://api-sandbox.asaas.com/v3 · Produção: https://api.asaas.com/v3</summary>
    public string Url { get; set; } = "https://api-sandbox.asaas.com/v3";

    /// <summary>Chave da conta geral (Pagamentos__Asaas__ApiKey); unidades podem ter chave própria no cadastro.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Token que o Asaas envia no cabeçalho "asaas-access-token" dos webhooks.</summary>
    public string WebhookToken { get; set; } = string.Empty;
}

public class GatewayException(string mensagem, Exception? inner = null) : Exception(mensagem, inner);
