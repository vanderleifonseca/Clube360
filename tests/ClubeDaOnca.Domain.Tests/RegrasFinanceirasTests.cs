using ClubeDaOnca.Domain.Financeiro;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Tests;

public class RegrasFinanceirasTests
{
    private static readonly ConfiguracaoFinanceira Cfg = new()
    {
        ValorIndividual = 150m, ValorFamiliar = 250m, ValorRemido = 0m, DiaVencimento = 10, DiasTolerancia = 5,
    };

    private static Socio Socio(CategoriaTitulo categoria = CategoriaTitulo.Familiar, SituacaoTitulo situacao = SituacaoTitulo.Ativo) =>
        new() { Nome = "Fulano", Categoria = categoria, Situacao = situacao };

    [Fact]
    public void Gera_mensalidade_com_valor_da_categoria_e_vencimento()
    {
        var c = RegrasFinanceiras.GerarMensalidade(Socio(), new DateOnly(2026, 11, 20), Cfg, jaExiste: false)!;
        Assert.Equal(250m, c.Valor);
        Assert.Equal(new DateOnly(2026, 11, 1), c.Competencia);
        Assert.Equal(new DateOnly(2026, 11, 10), c.Vencimento);
        Assert.Equal("Mensalidade Novembro/2026", c.Descricao);
        Assert.Equal(TipoCobranca.Mensalidade, c.Tipo);
    }

    [Fact]
    public void Nao_gera_para_remido_inativo_ou_duplicada()
    {
        Assert.Null(RegrasFinanceiras.GerarMensalidade(Socio(CategoriaTitulo.Remido), new DateOnly(2026, 11, 1), Cfg, false));
        Assert.Null(RegrasFinanceiras.GerarMensalidade(Socio(situacao: SituacaoTitulo.Suspenso), new DateOnly(2026, 11, 1), Cfg, false));
        Assert.Null(RegrasFinanceiras.GerarMensalidade(Socio(), new DateOnly(2026, 11, 1), Cfg, jaExiste: true));
    }

    [Fact]
    public void Vencimento_em_mes_curto_cai_no_ultimo_dia() =>
        Assert.Equal(new DateOnly(2027, 2, 28), RegrasFinanceiras.Vencimento(new DateOnly(2027, 2, 1), 31));

    [Fact]
    public void Taxa_de_reserva_vence_ate_a_data_da_reserva()
    {
        var hoje = new DateOnly(2026, 11, 8);
        Assert.Equal(new DateOnly(2026, 11, 11), RegrasFinanceiras.VencimentoTaxaReserva(hoje, new DateOnly(2026, 11, 30), 3));
        Assert.Equal(new DateOnly(2026, 11, 9), RegrasFinanceiras.VencimentoTaxaReserva(hoje, new DateOnly(2026, 11, 9), 3));
    }

    [Fact]
    public void Inadimplencia_considera_tolerancia()
    {
        var aberta = new Cobranca { Vencimento = new DateOnly(2026, 11, 10), Status = StatusCobranca.Aberta };
        Assert.False(RegrasFinanceiras.Inadimplente([aberta], new DateOnly(2026, 11, 15), 5));
        Assert.True(RegrasFinanceiras.Inadimplente([aberta], new DateOnly(2026, 11, 16), 5));

        var paga = new Cobranca { Vencimento = new DateOnly(2026, 10, 10), Status = StatusCobranca.Paga };
        Assert.False(RegrasFinanceiras.Inadimplente([paga], new DateOnly(2026, 11, 16), 5));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]  // só avisa o porteiro
    [InlineData(true, true, false)]  // bloqueia
    public void Portaria_considera_inadimplencia(bool inadimplente, bool bloquear, bool liberado)
    {
        var d = RegrasAcesso.Avaliar(SituacaoTitulo.Ativo, true, inadimplente, bloquear);
        Assert.Equal(liberado, d.Liberado);
        if (inadimplente) Assert.Contains("atraso", d.Motivo);
    }

    [Fact]
    public void Cobranca_avulsa_valida_vira_cobranca_aberta_do_tipo_avulsa()
    {
        var hoje = new DateOnly(2026, 11, 12);
        var (c, erro) = RegrasFinanceiras.CobrancaAvulsa(Socio(), "  Multa por dano ao patrimônio ", 85.50m, hoje.AddDays(10), hoje);
        Assert.Null(erro);
        Assert.Equal(TipoCobranca.Avulsa, c!.Tipo);
        Assert.Equal("Multa por dano ao patrimônio", c.Descricao);
        Assert.Equal(85.50m, c.Valor);
        Assert.Equal(hoje.AddDays(10), c.Vencimento);
        Assert.Equal(StatusCobranca.Aberta, c.Status);
        Assert.Null(c.Competencia);
    }

    [Theory]
    [InlineData("ab", 10, 0, SituacaoTitulo.Ativo)]          // descrição curta
    [InlineData("Consumo no bar", 0, 0, SituacaoTitulo.Ativo)]      // valor zero
    [InlineData("Consumo no bar", -5, 0, SituacaoTitulo.Ativo)]     // valor negativo
    [InlineData("Consumo no bar", 100001, 0, SituacaoTitulo.Ativo)] // acima do máximo
    [InlineData("Consumo no bar", 10.555, 0, SituacaoTitulo.Ativo)] // mais de 2 casas
    [InlineData("Consumo no bar", 10, -1, SituacaoTitulo.Ativo)]    // vencimento no passado
    [InlineData("Consumo no bar", 10, 0, SituacaoTitulo.Cancelado)]
    [InlineData("Consumo no bar", 10, 0, SituacaoTitulo.EmAdesao)]
    public void Cobranca_avulsa_invalida_devolve_erro(string descricao, double valor, int diasVencimento, SituacaoTitulo situacao)
    {
        var hoje = new DateOnly(2026, 11, 12);
        var (c, erro) = RegrasFinanceiras.CobrancaAvulsa(Socio(situacao: situacao), descricao, (decimal)valor, hoje.AddDays(diasVencimento), hoje);
        Assert.Null(c);
        Assert.False(string.IsNullOrWhiteSpace(erro));
    }

    [Fact]
    public void Cobranca_avulsa_aceita_titulo_suspenso_e_vencimento_hoje()
    {
        var hoje = new DateOnly(2026, 11, 12);
        var (c, erro) = RegrasFinanceiras.CobrancaAvulsa(Socio(situacao: SituacaoTitulo.Suspenso), "Conserto do portão", 300m, hoje, hoje);
        Assert.Null(erro);
        Assert.NotNull(c);
    }
}
