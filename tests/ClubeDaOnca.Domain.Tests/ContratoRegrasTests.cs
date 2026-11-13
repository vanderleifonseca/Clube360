using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Comum;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Tests;

public class ContratoRegrasTests
{
    [Theory]
    [InlineData(150, "cento e cinquenta reais")]
    [InlineData(100, "cem reais")]
    [InlineData(1, "um real")]
    [InlineData(0.01, "um centavo")]
    [InlineData(250.5, "duzentos e cinquenta reais e cinquenta centavos")]
    [InlineData(1200, "mil e duzentos reais")]
    [InlineData(1250, "mil duzentos e cinquenta reais")]
    [InlineData(2005.99, "dois mil e cinco reais e noventa e nove centavos")]
    [InlineData(1000000, "um milhão de reais")]
    [InlineData(21, "vinte e um reais")]
    [InlineData(0, "zero real")]
    public void Valor_por_extenso(double valor, string esperado) =>
        Assert.Equal(esperado, ValorPorExtenso.Reais((decimal)valor));

    [Fact]
    public void Transicoes_do_contrato()
    {
        Assert.True(RegrasContrato.PodeMudar(StatusContrato.Rascunho, StatusContrato.AguardandoAceite));
        Assert.True(RegrasContrato.PodeMudar(StatusContrato.AguardandoAceite, StatusContrato.EmAnalise));
        Assert.True(RegrasContrato.PodeMudar(StatusContrato.EmAnalise, StatusContrato.Aprovado));
        Assert.True(RegrasContrato.PodeMudar(StatusContrato.Aprovado, StatusContrato.Desligado));
        Assert.False(RegrasContrato.PodeMudar(StatusContrato.Rascunho, StatusContrato.Aprovado));
        Assert.False(RegrasContrato.PodeMudar(StatusContrato.AguardandoAceite, StatusContrato.Aprovado));
        Assert.False(RegrasContrato.PodeMudar(StatusContrato.Reprovado, StatusContrato.Aprovado));
        Assert.False(RegrasContrato.PodeMudar(StatusContrato.Desligado, StatusContrato.Aprovado));
    }

    [Fact]
    public void Vigencia_de_doze_meses_e_primeiro_vencimento()
    {
        Assert.Equal((new DateOnly(2026, 11, 11), new DateOnly(2027, 11, 10)), RegrasContrato.Vigencia(new DateOnly(2026, 11, 11)));
        Assert.Equal(new DateOnly(2026, 12, 10), RegrasContrato.PrimeiroVencimento(new DateOnly(2026, 11, 11), 10));
        Assert.Equal(new DateOnly(2027, 1, 10), RegrasContrato.PrimeiroVencimento(new DateOnly(2026, 12, 31), 10));
        Assert.Equal("2026/0007", RegrasContrato.Numero(2026, 7));
    }

    [Fact]
    public void Pendencias_do_cadastro_para_o_contrato()
    {
        var socio = new Socio { Nome = "X", Email = "x@x.com" };
        var pendencias = RegrasContrato.PendenciasCadastro(socio);
        Assert.Contains("RG", pendencias);
        Assert.Contains("Estado civil", pendencias);
        Assert.Contains("Endereço — CEP", pendencias);

        socio.Nacionalidade = "Brasileira";
        socio.EstadoCivil = EstadoCivil.Solteiro;
        socio.Profissao = "Professor";
        socio.Rg = "123";
        socio.Endereco = new Endereco { Cep = "85851000", Logradouro = "Rua A", Numero = "1", Bairro = "Centro", Cidade = "Foz", Uf = "PR" };
        Assert.Empty(RegrasContrato.PendenciasCadastro(socio));
    }

    [Fact]
    public void Candidato_em_adesao_nao_entra_na_portaria() =>
        Assert.False(RegrasAcesso.Avaliar(SituacaoTitulo.EmAdesao).Liberado);
}
