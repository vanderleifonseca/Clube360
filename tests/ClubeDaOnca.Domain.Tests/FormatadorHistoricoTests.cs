using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Auditoria;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Tests;

public class FormatadorHistoricoTests
{
    [Fact]
    public void Formata_valores_em_portugues()
    {
        Assert.Equal("15/03/1990", FormatadorHistorico.Formatar(new DateOnly(1990, 3, 15)));
        Assert.Equal("Sim", FormatadorHistorico.Formatar(true));
        Assert.Equal("Não informado", FormatadorHistorico.Formatar(Genero.NaoInformado));
        Assert.Equal("Cônjuge", FormatadorHistorico.Formatar(Parentesco.Conjuge));
        Assert.Null(FormatadorHistorico.Formatar("  "));
    }

    [Fact]
    public void Diferencas_ignoram_campos_tecnicos_e_valores_iguais()
    {
        var linhas = FormatadorHistorico.Diferencas([
            ("Id", Guid.NewGuid(), Guid.NewGuid()),
            ("Telefone", "1111", "2222"),
            ("Cidade", "Foz", "Foz"),
            ("Complemento", null, ""),
            ("VersaoChaveQr", 1, 2),
        ]).ToList();

        Assert.Equal(2, linhas.Count);
        Assert.Contains(("Telefone", "1111", "2222"), linhas);
        Assert.Contains(("QR Code", (string?)null, "Renovado"), linhas);
    }

    [Fact]
    public void Endereco_vira_texto_legivel()
    {
        var e = new Endereco
        {
            Logradouro = "Av. Brasil", Numero = "1200", Complemento = "Ap 31", Bairro = "Centro",
            Cidade = "Foz do Iguaçu", Uf = "PR", Cep = "85851000",
        };
        Assert.Equal("Av. Brasil, 1200 - Ap 31, Centro, Foz do Iguaçu/PR, CEP 85851-000", e.ToString());
        Assert.Equal(string.Empty, new Endereco().ToString());
    }
}
