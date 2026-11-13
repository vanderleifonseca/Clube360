using System.Text;
using ClubeDaOnca.Domain.Carteirinha;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Tests;

public class QrCarteirinhaTests
{
    private static readonly byte[] Mestra = Encoding.UTF8.GetBytes("chave-mestra-de-teste-com-32-caracteres");
    private static readonly Guid Pessoa = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Chave(int versao = 1) => QrCarteirinha.DerivarChave(Mestra, Pessoa, versao);

    [Fact]
    public void Codigo_recem_gerado_e_valido()
    {
        var token = QrCarteirinha.Gerar(Pessoa, Chave(), Agora);
        Assert.Equal(FalhaQr.Nenhuma, QrCarteirinha.Validar(token, Chave(), Agora));
        Assert.True(QrCarteirinha.TentarLer(token, out var leitura));
        Assert.Equal(Pessoa, leitura.PessoaId);
    }

    [Fact]
    public void Tolera_pequena_diferenca_de_relogio()
    {
        var token = QrCarteirinha.Gerar(Pessoa, Chave(), Agora);
        Assert.Equal(FalhaQr.Nenhuma, QrCarteirinha.Validar(token, Chave(), Agora.AddSeconds(30)));
        Assert.Equal(FalhaQr.Nenhuma, QrCarteirinha.Validar(token, Chave(), Agora.AddSeconds(-30)));
    }

    [Fact]
    public void Print_antigo_expira()
    {
        var token = QrCarteirinha.Gerar(Pessoa, Chave(), Agora);
        Assert.Equal(FalhaQr.Expirado, QrCarteirinha.Validar(token, Chave(), Agora.AddMinutes(2)));
    }

    [Fact]
    public void Trocar_a_versao_da_chave_invalida_codigos_antigos()
    {
        var token = QrCarteirinha.Gerar(Pessoa, Chave(versao: 1), Agora);
        Assert.Equal(FalhaQr.AssinaturaInvalida, QrCarteirinha.Validar(token, Chave(versao: 2), Agora));
    }

    [Fact]
    public void Codigo_adulterado_e_rejeitado()
    {
        var token = QrCarteirinha.Gerar(Pessoa, Chave(), Agora);
        var outraPessoa = token.Replace(Pessoa.ToString("N"), Guid.NewGuid().ToString("N"));
        Assert.Equal(FalhaQr.AssinaturaInvalida, QrCarteirinha.Validar(outraPessoa, Chave(), Agora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qualquer coisa")]
    [InlineData("CO1.nao-e-guid.1.abc")]
    public void Formato_invalido(string? token) =>
        Assert.Equal(FalhaQr.FormatoInvalido, QrCarteirinha.Validar(token, Chave(), Agora));

    [Theory]
    [InlineData(SituacaoTitulo.Ativo, true, true)]
    [InlineData(SituacaoTitulo.Ativo, false, false)]
    [InlineData(SituacaoTitulo.Suspenso, true, false)]
    [InlineData(SituacaoTitulo.Cancelado, true, false)]
    public void Regras_de_acesso(SituacaoTitulo situacao, bool dependenteAtivo, bool liberado) =>
        Assert.Equal(liberado, RegrasAcesso.Avaliar(situacao, dependenteAtivo).Liberado);
}
