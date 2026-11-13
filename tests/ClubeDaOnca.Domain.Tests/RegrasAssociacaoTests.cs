using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Comum;

namespace ClubeDaOnca.Domain.Tests;

public class RegrasAssociacaoTests
{
    private static MandatoPresidente Mandato(DateOnly inicio) =>
        new() { Nome = "P", Inicio = inicio, Fim = RegrasAssociacao.FimDoMandato(inicio) };

    [Fact]
    public void Mandato_dura_dois_anos() =>
        Assert.Equal(new DateOnly(2028, 12, 31), RegrasAssociacao.FimDoMandato(new DateOnly(2027, 1, 1)));

    [Fact]
    public void Detecta_sobreposicao_e_aceita_mandato_seguinte()
    {
        var atual = Mandato(new DateOnly(2026, 1, 1));
        Assert.True(RegrasAssociacao.Sobrepoe(new DateOnly(2027, 6, 1), new DateOnly(2029, 5, 31), [atual]));
        Assert.False(RegrasAssociacao.Sobrepoe(new DateOnly(2028, 1, 1), new DateOnly(2029, 12, 31), [atual]));
        Assert.False(RegrasAssociacao.Sobrepoe(atual.Inicio, atual.Fim, [atual], ignorar: atual.Id));
    }

    [Fact]
    public void Avisa_mandato_vencendo_sem_sucessor()
    {
        var atual = Mandato(new DateOnly(2026, 1, 1)); // termina em 31/12/2027
        var hoje = new DateOnly(2027, 11, 15);
        Assert.Same(atual, RegrasAssociacao.PresidenteEm([atual], hoje));
        Assert.True(RegrasAssociacao.MandatoVencendo([atual], hoje));
        Assert.False(RegrasAssociacao.MandatoVencendo([atual], new DateOnly(2027, 6, 1)));
        Assert.False(RegrasAssociacao.MandatoVencendo([atual, Mandato(new DateOnly(2028, 1, 1))], hoje));
    }

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000181", true)]
    [InlineData("11.222.333/0001-80", false)]
    [InlineData("11111111111111", false)]
    public void Valida_cnpj(string cnpj, bool valido) => Assert.Equal(valido, Cnpj.EhValido(cnpj));
}
