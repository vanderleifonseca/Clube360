using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Financeiro;

namespace ClubeDaOnca.Domain.Tests;

public class ParcelasContratoTests
{
    private static Contrato Contrato(decimal valor = 250m) => new()
    {
        Numero = "2026/0001", SocioId = Guid.NewGuid(), AssociacaoId = Guid.NewGuid(),
        ValorMensalidade = valor, DiaVencimento = 31, PrimeiroVencimento = new DateOnly(2026, 12, 31),
        Status = StatusContrato.Aprovado, VigenciaInicio = new DateOnly(2026, 11, 11), VigenciaFim = new DateOnly(2027, 11, 10),
    };

    [Fact]
    public void Gera_doze_parcelas_mensais_numeradas()
    {
        var c = Contrato();
        var parcelas = RegrasFinanceiras.ParcelasContrato(c, new HashSet<DateOnly>());

        Assert.Equal(12, parcelas.Count);
        Assert.Equal(Enumerable.Range(1, 12), parcelas.Select(p => p.Parcela!.Value));
        Assert.All(parcelas, p => Assert.Equal(c.Id, p.ContratoId));
        Assert.Equal(new DateOnly(2026, 12, 31), parcelas[0].Vencimento);
        Assert.Equal(new DateOnly(2027, 2, 28), parcelas[2].Vencimento); // fevereiro: último dia do mês
        Assert.Equal(new DateOnly(2027, 11, 1), parcelas[11].Competencia);
        Assert.Contains("parcela 1/12", parcelas[0].Descricao);
    }

    [Fact]
    public void Pula_meses_ja_cobrados_e_contrato_sem_valor()
    {
        var parcelas = RegrasFinanceiras.ParcelasContrato(Contrato(), new HashSet<DateOnly> { new(2027, 1, 1) });
        Assert.Equal(11, parcelas.Count);
        Assert.DoesNotContain(parcelas, p => p.Competencia == new DateOnly(2027, 1, 1));
        Assert.Empty(RegrasFinanceiras.ParcelasContrato(Contrato(0m), new HashSet<DateOnly>()));
    }

    [Fact]
    public void Contrato_aprovado_cobre_os_meses_da_vigencia()
    {
        var c = Contrato();
        Assert.True(RegrasFinanceiras.ContratoCobre(c, new DateOnly(2026, 11, 1)));
        Assert.True(RegrasFinanceiras.ContratoCobre(c, new DateOnly(2027, 11, 1)));
        Assert.False(RegrasFinanceiras.ContratoCobre(c, new DateOnly(2026, 10, 1)));
        c.Status = StatusContrato.Desligado;
        Assert.False(RegrasFinanceiras.ContratoCobre(c, new DateOnly(2027, 3, 1)));
    }
}
