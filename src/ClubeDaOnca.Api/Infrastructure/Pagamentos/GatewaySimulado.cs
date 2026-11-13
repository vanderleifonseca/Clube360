using System.Security.Cryptography;
using ClubeDaOnca.Domain.Financeiro;

namespace ClubeDaOnca.Api.Infrastructure.Pagamentos;

/// <summary>
/// Gateway de mentira para desenvolvimento e testes: gera Pix/boleto fictícios.
/// A confirmação é feita pelo endpoint de simulação (só existe com este gateway).
/// </summary>
public sealed class GatewaySimulado : IGatewayPagamento
{
    public string Nome => "Simulado";
    public bool Simulado => true;

    public Task<CobrancaGateway> CriarAsync(DadosPagador pagador, Cobranca cobranca, FormaPagamento forma,
        DateOnly vencimento, CancellationToken ct)
    {
        var id = $"sim_{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
        var resultado = forma == FormaPagamento.Pix
            ? new CobrancaGateway(id,
                $"00020126SIMULADO-CLUBEDAONCA-{id}-{cobranca.Valor:0.00}5204000053039865802BR6304ABCD",
                null, DateTimeOffset.UtcNow.AddHours(24), null, null)
            : new CobrancaGateway(id, null, null, null, $"https://sandbox.exemplo/boleto/{id}",
                "23793.38128 60000.000003 00000.000400 1 " + vencimento.DayNumber.ToString().PadLeft(4, '0') +
                ((long)(cobranca.Valor * 100)).ToString().PadLeft(10, '0'));
        return Task.FromResult(resultado);
    }

    public Task CancelarAsync(string idExterno, CancellationToken ct) => Task.CompletedTask;
}
