using ClubeDaOnca.Domain.Comum;

namespace ClubeDaOnca.Domain.Tests;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Aceita_cpf_valido(string cpf) => Assert.True(Cpf.EhValido(cpf));

    [Theory]
    [InlineData("529.982.247-24")] // dígito verificador errado
    [InlineData("111.111.111-11")] // todos iguais
    [InlineData("123")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejeita_cpf_invalido(string? cpf) => Assert.False(Cpf.EhValido(cpf));

    [Fact]
    public void Normalizar_remove_mascara() => Assert.Equal("52998224725", Cpf.Normalizar("529.982.247-25"));
}
